START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    ALTER TABLE "CashSessions" ADD "TerminalId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    ALTER TABLE "CashSessions" ADD "TimeZoneSnapshot" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    ALTER TABLE "CashSessions" ADD "ClosedByUserName" character varying(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    CREATE INDEX "IX_CashSessions_TerminalId" ON "CashSessions" ("TerminalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    CREATE UNIQUE INDEX "IX_CashSessions_BranchId_TerminalId" ON "CashSessions" ("BranchId", "TerminalId") WHERE "Status" = 1 AND "TerminalId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    ALTER TABLE "CashSessions" ADD CONSTRAINT "FK_CashSessions_PosTerminals_TerminalId" FOREIGN KEY ("TerminalId") REFERENCES "PosTerminals" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    CREATE TABLE "CashCommandReceipts" (
        "ScopeKey" character varying(64) NOT NULL,
        "Fingerprint" character varying(64) NOT NULL,
        "SessionId" uuid NOT NULL,
        "CompletedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_CashCommandReceipts" PRIMARY KEY ("ScopeKey"),
        CONSTRAINT "FK_CashCommandReceipts_CashSessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES "CashSessions" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    CREATE INDEX "IX_CashCommandReceipts_SessionId" ON "CashCommandReceipts" ("SessionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    CREATE UNIQUE INDEX "IX_CashMovements_ManualReversal" ON "CashMovements" ("SourceId") WHERE "SourceType" = 'CashMovementReversal' AND "SourceId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    CREATE UNIQUE INDEX "IX_CashMovements_AutomaticPayment" ON "CashMovements" ("SourceType", "SourceId") WHERE "SourceType" IN ('SalePayment', 'SaleRefund') AND "SourceId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024090000_PosCashContextAndCommands') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024090000_PosCashContextAndCommands', '10.0.10');
    END IF;
END $EF$;
COMMIT;

