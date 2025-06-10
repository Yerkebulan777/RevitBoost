@echo off
REM setup.bat - Автоматическая установка для Windows
echo ============================================
echo   PostgreSQL Distributed Sync Setup
echo ============================================

REM Проверка наличия Docker
docker --version >nul 2>&1
if errorlevel 1 (
    echo ОШИБКА: Docker не установлен!
    echo Установите Docker Desktop: https://www.docker.com/products/docker-desktop
    pause
    exit /b 1
)

echo [1/4] Создание структуры папок...
if not exist "init-scripts" mkdir init-scripts
if not exist "config" mkdir config
if not exist "scripts" mkdir scripts

echo [2/4] Создание docker-compose.yml...
(
echo version: '3.8'^

echo:
echo services:
echo   postgresql:
echo     image: postgres:17.5
echo     container_name: postgres_distributed_sync
echo     restart: unless-stopped
echo     environment:
echo       POSTGRES_DB: distributed_sync
echo       POSTGRES_USER: sync_admin
echo       POSTGRES_PASSWORD: SecurePassword123!
echo     ports:
echo       - "5432:5432"
echo     volumes:
echo       - postgres_data:/var/lib/postgresql/data
echo       - ./init-scripts:/docker-entrypoint-initdb.d:ro
echo     healthcheck:
echo       test: ["CMD-SHELL", "pg_isready -U sync_admin -d distributed_sync"]
echo       interval: 10s
echo       timeout: 5s
echo       retries: 5
echo:
echo   pgadmin:
echo     image: dpage/pgadmin4:latest
echo     container_name: pgadmin_distributed_sync
echo     restart: unless-stopped
echo     environment:
echo       PGADMIN_DEFAULT_EMAIL: admin@company.local
echo       PGADMIN_DEFAULT_PASSWORD: AdminPassword123!
echo     ports:
echo       - "8080:80"
echo     volumes:
echo       - pgadmin_data:/var/lib/pgadmin
echo     depends_on:
echo       postgresql:
echo         condition: service_healthy
echo:
echo volumes:
echo   postgres_data:
echo   pgadmin_data:
) > docker-compose.yml

echo [3/4] Создание SQL скриптов...
(
echo CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
echo CREATE EXTENSION IF NOT EXISTS "pgcrypto";
echo CREATE ROLE app_user LOGIN PASSWORD 'AppPassword123!';
echo CREATE SCHEMA IF NOT EXISTS sync_schema AUTHORIZATION app_user;
echo SET search_path TO sync_schema;
echo CREATE TABLE IF NOT EXISTS distributed_locks ^(
echo     lock_name VARCHAR^(255^) PRIMARY KEY,
echo     owner_id UUID NOT NULL,
echo     acquired_at TIMESTAMP WITH TIME ZONE DEFAULT NOW^(^),
echo     expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
echo     process_info JSONB NOT NULL DEFAULT '{}'::jsonb
echo ^);
echo CREATE INDEX IF NOT EXISTS idx_distributed_locks_expires_at ON distributed_locks^(expires_at^);
echo GRANT SELECT, INSERT, UPDATE, DELETE ON distributed_locks TO app_user;
) > init-scripts\01_init_database.sql

echo [4/4] Создание управляющих скриптов...
(
echo @echo off
echo docker compose up -d
echo docker compose ps
echo echo Доступ к pgAdmin: http://localhost:8080
echo pause
) > scripts\start.bat

(
echo @echo off
echo docker compose down
echo pause
) > scripts\stop.bat

echo ============================================
echo Установка завершена успешно!
echo ============================================
echo:
echo Запуск: scripts\start.bat
echo Остановка: scripts\stop.bat
echo pgAdmin: http://localhost:8080
echo:
pause