FROM mcr.microsoft.com/mssql/server:2022-latest
USER root
RUN mkdir -p /.system && chown -R mssql:mssql /.system && chmod 755 /.system
USER mssql
