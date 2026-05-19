-- ==============================================================
-- PlatformAI - Seed ProductionOrder + Events + ProductionData
-- Presuppone: Linea A con 4 macchine già presenti
-- Aggiunge un nuovo ordine con i suoi dati (non cancella nulla)
-- ==============================================================

USE TestApplicationAI;
SET NOCOUNT ON;

DECLARE @StartTime DATETIME2 = DATEADD(DAY, -1, CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2));
SET @StartTime = DATEADD(HOUR, 8, @StartTime);   -- ieri 08:00
DECLARE @StopTime  DATETIME2 = DATEADD(HOUR, 8, @StartTime); -- ieri 16:00
DECLARE @Now       DATETIME2 = SYSUTCDATETIME();

-- ==============================================================
-- Recupera la linea e le macchine esistenti
-- ==============================================================
DECLARE @LineId UNIQUEIDENTIFIER;
SELECT TOP 1 @LineId = Id FROM ProductionLines WHERE Name = 'Linea A';

IF @LineId IS NULL
BEGIN
    RAISERROR('Linea A non trovata. Esegui prima TestData.sql.', 16, 1);
    RETURN;
END

DECLARE @machines TABLE (Id UNIQUEIDENTIFIER, Code NVARCHAR(20), RowNum INT);
INSERT INTO @machines
SELECT Id, Code, ROW_NUMBER() OVER (ORDER BY Code)
FROM Machines
WHERE ProductionLineId = @LineId;

IF (SELECT COUNT(*) FROM @machines) = 0
BEGIN
    RAISERROR('Nessuna macchina trovata su Linea A.', 16, 1);
    RETURN;
END

DECLARE @MachineCount INT;
SELECT @MachineCount = COUNT(*) FROM @machines;
PRINT CONCAT('Linea trovata: ', CAST(@LineId AS NVARCHAR(36)));
PRINT CONCAT('Macchine trovate: ', @MachineCount);

-- ==============================================================
-- Calcola il prossimo numero ordine
-- ==============================================================
DECLARE @NextOrderNum INT;
SELECT @NextOrderNum = ISNULL(
    MAX(CAST(SUBSTRING(OrderNumber, 5, 10) AS INT)), 0
) + 1
FROM ProductionOrders;

DECLARE @NextOrderCode NVARCHAR(20) = CONCAT('ORD-', FORMAT(@NextOrderNum, '00000'));

-- ==============================================================
-- 5. ProductionOrder
-- ==============================================================
DECLARE @OrderId UNIQUEIDENTIFIER = NEWID();
INSERT INTO ProductionOrders (Id, OrderNumber, ProductCode, PlannedQuantity, StartTime, EndTime, ProductionLineId, UserCreate, CreateDate, UserModify, LastModifiedDate)
VALUES (@OrderId, @NextOrderCode, 'PRD-001', 500, @StartTime, @StopTime, @LineId, 'system', @Now, 'system', @Now);

PRINT CONCAT('Ordine creato: ', @NextOrderCode, ' (', CAST(@OrderId AS NVARCHAR(36)), ')');

-- ==============================================================
-- 6. MachineEvent — START e STOP per ogni macchina
-- ==============================================================
DECLARE @MId UNIQUEIDENTIFIER;
DECLARE @MCode NVARCHAR(20);
DECLARE mc CURSOR FOR SELECT Id, Code FROM @machines ORDER BY Code;
OPEN mc;
FETCH NEXT FROM mc INTO @MId, @MCode;

WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO MachineEvent (Id, MachineId, EventType, EventTime, Message, UserCreate, CreateDate, UserModify, LastModifiedDate)
    VALUES
    (NEWID(), @MId, 'START', @StartTime, CONCAT(@MCode, ' - Avvio turno mattina'), 'system', @Now, 'system', @Now),
    (NEWID(), @MId, 'STOP',  @StopTime,  CONCAT(@MCode, ' - Fine turno mattina'),  'system', @Now, 'system', @Now);

    PRINT CONCAT('  Eventi START/STOP inseriti per ', @MCode);

    FETCH NEXT FROM mc INTO @MId, @MCode;
END;
CLOSE mc;
DEALLOCATE mc;

-- ==============================================================
-- 7. ProductionData — ogni 30 minuti tra START e STOP
--    15 record per macchina (08:30 → 16:00)
-- ==============================================================
DECLARE mc2 CURSOR FOR SELECT Id, Code FROM @machines ORDER BY Code;
OPEN mc2;
FETCH NEXT FROM mc2 INTO @MId, @MCode;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @slot INT = 1;
    WHILE @slot <= 15
    BEGIN
        DECLARE @ts DATETIME2 = DATEADD(MINUTE, @slot * 30, @StartTime);

        INSERT INTO ProductionData (
            Id, MachineId, ProductionOrderId, [Timestamp],
            QuantityProduced, ScrapQuantity, CycleTime,
            EnergyConsumption, Temperature,
            UserCreate, CreateDate, UserModify, LastModifiedDate
        )
        VALUES (
            NEWID(),
            @MId,
            @OrderId,
            @ts,
            80 + (ABS(CHECKSUM(NEWID())) % 41),       -- pezzi: 80-120
            ABS(CHECKSUM(NEWID())) % 6,                -- scarti: 0-5
            1.5 + (ABS(CHECKSUM(NEWID())) % 20) / 10.0, -- cycle time: 1.5-3.5s
            80.0 + (ABS(CHECKSUM(NEWID())) % 41),      -- energia: 80-120 kWh
            35.0 + (ABS(CHECKSUM(NEWID())) % 21),      -- temp: 35-55°C
            'system', @Now, 'system', @Now
        );

        SET @slot += 1;
    END;

    PRINT CONCAT('  15 record ProductionData inseriti per ', @MCode);
    FETCH NEXT FROM mc2 INTO @MId, @MCode;
END;
CLOSE mc2;
DEALLOCATE mc2;

-- ==============================================================
DECLARE @TotalPD INT;
DECLARE @TotalEv INT;
SELECT @TotalPD = COUNT(*) FROM ProductionData;
SELECT @TotalEv = COUNT(*) FROM MachineEvent;
PRINT '✅ Seed completato:';
PRINT CONCAT('   ProductionData:  ', @TotalPD, ' record');
PRINT CONCAT('   MachineEvent:    ', @TotalEv, ' record');
PRINT '   Periodo: ieri 08:00 → 16:00, ogni 30 min';
