#!/bin/bash
set -e

mkdir -p /var/opt/mssql/data /var/opt/mssql/log /var/opt/mssql/secrets /var/opt/mssql/backup
chown -R mssql:mssql /var/opt/mssql
chmod -R 770 /var/opt/mssql

exec su -s /bin/bash -c "exec /opt/mssql/bin/sqlservr" mssql
