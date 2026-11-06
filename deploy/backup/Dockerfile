FROM postgres:18-alpine@sha256:10a5c69a83035ab5239f5620d708520e8b28f5aa1f2b20143e6024532edadfb5

ARG BILLWATCH_RELEASE_ID=unknown

LABEL org.opencontainers.image.revision="$BILLWATCH_RELEASE_ID"

RUN apk add --no-cache \
        ca-certificates \
        jq \
        restic \
    && mkdir --parents /cache /repository /work \
    && chown --recursive 1654:1654 /cache /repository /work

COPY backup.sh /usr/local/bin/billwatch-backup
COPY permission-policy.sh /usr/local/lib/fullworth/backup-permission-policy.sh

RUN chmod 0555 \
        /usr/local/bin/billwatch-backup \
        /usr/local/lib/fullworth/backup-permission-policy.sh

ENV HOME=/work \
    TMPDIR=/work \
    RESTIC_CACHE_DIR=/cache

USER 1654:1654

ENTRYPOINT ["/usr/local/bin/billwatch-backup"]
CMD ["backup"]
