FROM postgres:18-alpine@sha256:123b17998ccaf47e53b502e8c6a1e79afd8f579c6f3eec83c563da026f957395

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
