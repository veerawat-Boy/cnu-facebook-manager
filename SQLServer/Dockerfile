FROM mcr.microsoft.com/mssql/server:2022-latest
USER root

RUN mkdir -p /.system /log /tmp/mssql && \
    chown -R mssql:mssql /.system /log /tmp/mssql && \
    chmod 755 /.system /log /tmp/mssql

COPY entrypoint.sh /entrypoint.sh
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
