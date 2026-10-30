using System.Security.Claims;
using System.Text;
using FullWorth.API.Controllers;
using FullWorth.API.Data;
using FullWorth.API.Data.Entities;
using FullWorth.API.Services.Bills;
using FullWorth.API.Services.Statements;
using FullWorth.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FullWorth.Tests.Services;

public sealed class StatementUploadWriteRecoveryTests
{
    public static IEnumerable<object[]> Failures()
    {
        foreach (var mode in new[] { "database", "cancel", "unexpected" })
        foreach (var callerChanges in new[] { false, true })
            yield return [mode, callerChanges];
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task FailedMetadataInsert_PreservesOriginalForOwnedDeletion_DetachesCandidate_AndRetries(
        string mode, bool callerChanges)
    {
        await using var factory = new FullWorthApiFactory();
        using var identityScope = factory.Services.CreateScope();
        var name = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot();
        var failure = new UploadFailure(mode);
        var options = new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase(name, root).AddInterceptors(failure).Options;
        var plain = new DbContextOptionsBuilder<FullWorthDbContext>().UseInMemoryDatabase(name, root).Options;
        var storageRoot = Path.Combine(Path.GetTempPath(), "FullWorth-upload-recovery", Guid.NewGuid().ToString());
        var storage = new SecureBillStatementStorageService(Options.Create(new BillStatementStorageOptions
            { RootPath = storageRoot, MaxFileSizeBytes = 1024 * 1024 }),
            factory.Services.GetRequiredService<IWebHostEnvironment>());
        try
        {
            await using var db = new FullWorthDbContext(options);
            var owner = Guid.NewGuid();
            var stream = new BillStreamEntity { UserId = owner, ProviderName = "Fixture" };
            var modified = new BankConnectionEntity { UserId = owner, InstitutionName = "Original" };
            var deleted = new BankConnectionEntity { UserId = owner, InstitutionName = "Deleted caller" };
            var added = new BankConnectionEntity { UserId = owner, InstitutionName = "Added caller" };
            db.AddRange(stream, modified, deleted);
            await db.SaveChangesAsync();
            if (callerChanges)
            {
                modified.InstitutionName = "Unsaved caller";
                db.Remove(deleted);
                db.Add(added);
            }
            db.ChangeTracker.DetectChanges();
            var states = db.ChangeTracker.Entries<BankConnectionEntity>().ToDictionary(x => x.Entity.Id, x => x.State);
            var flags = db.Entry(modified).Properties.Where(x => x.IsModified).Select(x => x.Metadata.Name).Order().ToArray();
            var controller = new BillStatementUploadsController(db,
                identityScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), storage,
                new BillStatementProcessingSignal(), new BillStreamReadGateway(db))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "fixture"))
                }}
            };
            using var stop = new CancellationTokenSource();
            failure.Cancel = stop.Cancel;
            failure.Armed = true;
            using var bytes = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nfixture document\n%%EOF"));
            var file = new FormFile(bytes, 0, bytes.Length, "file", "fixture.pdf");
            var error = await Record.ExceptionAsync(() => controller.UploadStatement(stream.Id, file, stop.Token));
            if (mode == "cancel") Assert.IsAssignableFrom<OperationCanceledException>(error);
            else if (mode == "database") Assert.IsType<DbUpdateException>(error);
            else Assert.IsType<InvalidOperationException>(error);
            Assert.NotNull(failure.Candidate);
            Assert.Equal(EntityState.Detached, db.Entry(failure.Candidate).State);
            Assert.Single(Directory.GetFiles(storageRoot, "*", SearchOption.AllDirectories));
            var deletion = new AccountStatementDeletionGateway(db, storage,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountStatementDeletionGateway>.Instance);
            var protectedEntries = await deletion.QuarantineOwnedFilesAsync(owner);
            var protectedEntry = Assert.Single(protectedEntries);
            Assert.True(protectedEntry.WasPresent);
            Assert.Equal(owner, protectedEntry.UserId);
            Assert.Empty(storage.GetOwnedStorageKeys(owner));
            deletion.RestoreQuarantine(protectedEntry);
            Assert.Single(storage.GetOwnedStorageKeys(owner));
            foreach (var (id, state) in states)
                Assert.Equal(state, db.ChangeTracker.Entries<BankConnectionEntity>().Single(x => x.Entity.Id == id).State);
            Assert.Equal(flags, db.Entry(modified).Properties.Where(x => x.IsModified)
                .Select(x => x.Metadata.Name).Order().ToArray());
            Assert.Equal("Original", db.Entry(modified).OriginalValues.GetValue<string>(nameof(modified.InstitutionName)));

            // A later save must not write metadata referencing the removed file.
            await db.SaveChangesAsync();
            await using (var verify = new FullWorthDbContext(plain))
            {
                Assert.Empty(await verify.BillStatementUploads.ToListAsync());
                Assert.Equal(callerChanges ? "Unsaved caller" : "Original",
                    (await verify.BankConnections.SingleAsync(x => x.Id == modified.Id)).InstitutionName);
                Assert.Equal(!callerChanges, await verify.BankConnections.AnyAsync(x => x.Id == deleted.Id));
                Assert.Equal(callerChanges, await verify.BankConnections.AnyAsync(x => x.Id == added.Id));
            }
            bytes.Position = 0;
            var result = await controller.UploadStatement(stream.Id, file);
            Assert.IsType<CreatedResult>(result.Result);
            var saved = await db.BillStatementUploads.SingleAsync();
            using var stored = storage.OpenRead(owner, saved.StorageKey);
            Assert.True(stored.Length > 8);
            Assert.Equal(BillStatementUploadStatus.Uploaded, saved.Status);
        }
        finally { Directory.Delete(storageRoot, recursive: true); }
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedInsert_WithLostAcknowledgement_PreservesFileAndReadableStatus(bool cancel)
    {
        await using var factory = new FullWorthApiFactory();
        using var identityScope = factory.Services.CreateScope();
        var name = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot();
        var failure = new LostUploadAcknowledgement(cancel);
        var options = new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase(name, root).AddInterceptors(failure).Options;
        var plain = new DbContextOptionsBuilder<FullWorthDbContext>().UseInMemoryDatabase(name, root).Options;
        var storageRoot = Path.Combine(Path.GetTempPath(), "FullWorth-upload-ack", Guid.NewGuid().ToString());
        var storage = new SecureBillStatementStorageService(Options.Create(new BillStatementStorageOptions
            { RootPath = storageRoot, MaxFileSizeBytes = 1024 * 1024 }),
            factory.Services.GetRequiredService<IWebHostEnvironment>());
        try
        {
            await using var db = new FullWorthDbContext(options);
            var owner = Guid.NewGuid();
            var stream = new BillStreamEntity { UserId = owner, ProviderName = "Fixture" };
            db.Add(stream);
            await db.SaveChangesAsync();
            var controller = new BillStatementUploadsController(db,
                identityScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), storage,
                new BillStatementProcessingSignal(), new BillStreamReadGateway(db))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, owner.ToString())], "fixture"))
                }}
            };
            using var stop = new CancellationTokenSource();
            failure.Cancel = stop.Cancel;
            failure.Armed = true;
            using var bytes = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nfixture document\n%%EOF"));
            var file = new FormFile(bytes, 0, bytes.Length, "file", "fixture.pdf");
            var error = await Record.ExceptionAsync(() => controller.UploadStatement(stream.Id, file, stop.Token));
            if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
            else Assert.IsType<InvalidOperationException>(error);
            Assert.NotNull(failure.Candidate);
            Assert.Equal(EntityState.Detached, db.Entry(failure.Candidate).State);
            await using var verify = new FullWorthDbContext(plain);
            var saved = await verify.BillStatementUploads.SingleAsync();
            Assert.Equal(owner, saved.UserId);
            Assert.Equal(stream.Id, saved.BillStreamId);
            using var stored = storage.OpenRead(owner, saved.StorageKey);
            Assert.True(stored.Length > 8);
            var latest = await controller.GetLatestUpload(stream.Id);
            var ok = Assert.IsType<OkObjectResult>(latest.Result);
            Assert.Equal(saved.Id, Assert.IsType<BillStatementUploadStatusResult>(ok.Value).Id);
            await db.SaveChangesAsync();
            Assert.Single(await verify.BillStatementUploads.AsNoTracking().ToListAsync());
        }
        finally { Directory.Delete(storageRoot, recursive: true); }
    }

    private sealed class LostUploadAcknowledgement(bool cancel) : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public Action? Cancel { get; set; }
        public BillStatementUploadEntity? Candidate { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed)
                Candidate = eventData.Context!.ChangeTracker.Entries<BillStatementUploadEntity>()
                    .Single(x => x.State == EntityState.Added).Entity;
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (!Armed) return ValueTask.FromResult(result);
            Armed = false;
            if (cancel) { Cancel!(); cancellationToken.ThrowIfCancellationRequested(); }
            throw new InvalidOperationException("Synthetic post-commit acknowledgement loss.");
        }
    }


    [Fact]
    public async Task OwnedDeletion_IncludesUnreferencedOriginals_ExcludesOtherOwners_AndRejectsUnexpectedFiles()
    {
        await using var factory = new FullWorthApiFactory();
        var root = Path.Combine(Path.GetTempPath(), "FullWorth-upload-owned", Guid.NewGuid().ToString());
        var storage = new SecureBillStatementStorageService(Options.Create(new BillStatementStorageOptions
            { RootPath = root, MaxFileSizeBytes = 1024 * 1024 }),
            factory.Services.GetRequiredService<IWebHostEnvironment>());
        var options = new DbContextOptionsBuilder<FullWorthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        try
        {
            var owner = Guid.NewGuid();
            var other = Guid.NewGuid();
            using var first = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nfixture"));
            using var second = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nother fixture"));
            var owned = await storage.StoreAsync(owner, first, "fixture.pdf");
            var foreign = await storage.StoreAsync(other, second, "fixture.pdf");
            Assert.Equal(owned.StorageKey, Assert.Single(storage.GetOwnedStorageKeys(owner)));
            await using var db = new FullWorthDbContext(options);
            var gateway = new AccountStatementDeletionGateway(db, storage,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountStatementDeletionGateway>.Instance);
            var entry = Assert.Single(await gateway.QuarantineOwnedFilesAsync(owner));
            Assert.Equal(owned.StorageKey, entry.StorageKey);
            gateway.CommitQuarantine(entry);
            Assert.Empty(storage.GetOwnedStorageKeys(owner));
            using var untouched = storage.OpenRead(other, foreign.StorageKey);
            Assert.True(untouched.Length > 8);
            Directory.CreateDirectory(Path.Combine(root, owner.ToString("N")));
            await File.WriteAllTextAsync(Path.Combine(root, owner.ToString("N"), "unexpected.txt"), "fixture");
            Assert.Throws<InvalidOperationException>(() => storage.GetOwnedStorageKeys(owner));
            await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.QuarantineOwnedFilesAsync(owner));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private sealed class UploadFailure(string mode) : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public Action? Cancel { get; set; }
        public BillStatementUploadEntity? Candidate { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Armed) return ValueTask.FromResult(result);
            Armed = false;
            Candidate = eventData.Context!.ChangeTracker.Entries<BillStatementUploadEntity>()
                .Single(x => x.State == EntityState.Added).Entity;
            if (mode == "cancel") { Cancel!(); cancellationToken.ThrowIfCancellationRequested(); }
            if (mode == "unexpected") throw new InvalidOperationException("Synthetic upload failure.");
            throw new DbUpdateException("Synthetic pre-commit metadata failure.");
        }
    }
}
