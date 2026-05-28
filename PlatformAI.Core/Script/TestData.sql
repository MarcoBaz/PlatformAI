-- ==============================================================
-- PlatformAI - Clean Seed Data
-- 1 linea di produzione, 4 macchine, START+STOP per macchina,
-- dati di produzione ogni 30 minuti tra gli eventi
-- ==============================================================
SET NOCOUNT ON;

-- Data di riferimento: ieri alle 08:00
DECLARE @StartTime DATETIME2 = DATEADD(DAY, -1, CAST(CAST(GETUTCDATE() AS DATE) AS DATETIME2));
SET @StartTime = DATEADD(HOUR, 8, @StartTime);   -- ieri 08:00
DECLARE @StopTime  DATETIME2 = DATEADD(HOUR, 8, @StartTime); -- ieri 16:00
DECLARE @Now       DATETIME2 = SYSUTCDATETIME();

-- ==============================================================
-- 🧹 PULIZIA - ordine rispettando le FK
-- ==============================================================
DELETE FROM ProductionData;
DELETE FROM MachineEvent;
DELETE FROM ProductionOrders;
DELETE FROM Machines;
DELETE FROM ProductionLines;
DELETE FROM Departments;
DELETE FROM CostCenters;

-- ==============================================================
-- 1. CostCenter
-- ==============================================================
DECLARE @CostCenterId UNIQUEIDENTIFIER = NEWID();
INSERT INTO CostCenters (Id, Code, [Name], HourlyCost, UserCreate, CreateDate, UserModify, LastModifiedDate)
VALUES (@CostCenterId, 'CC-001', 'Assemblaggio', 50.00, 'system', @Now, 'system', @Now);

-- ==============================================================
-- 2. Department
-- ==============================================================
DECLARE @DepartmentId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Departments (Id, Code, [Name], TenantCode, [Description], IsActive, UserCreate, CreateDate, UserModify, LastModifiedDate)
VALUES (@DepartmentId, 'DPT-001', 'Reparto Assemblaggio', 'TENANT-001', 'Montaggio e test componenti', 1, 'system', @Now, 'system', @Now);

-- ==============================================================
-- 3. ProductionLine (1 sola)
-- ==============================================================
DECLARE @LineId UNIQUEIDENTIFIER = NEWID();
INSERT INTO ProductionLines (Id, Name, [Description], IsActive, UserCreate, CreateDate, UserModify, LastModifiedDate, DepartmentId)
VALUES (@LineId, 'Linea A', 'Linea principale assemblaggio', 1, 'system', @Now, 'system', @Now, @DepartmentId);

-- ==============================================================
-- 4. Machines (4 sulla linea)
-- ==============================================================
DECLARE @MachineId1 UNIQUEIDENTIFIER = NEWID();
DECLARE @MachineId2 UNIQUEIDENTIFIER = NEWID();
DECLARE @MachineId3 UNIQUEIDENTIFIER = NEWID();
DECLARE @MachineId4 UNIQUEIDENTIFIER = NEWID();

INSERT INTO Machines (Id, ProductionLineId, Code, Name, Type, Status, UserCreate, CreateDate, UserModify, LastModifiedDate)
VALUES
(@MachineId1, @LineId, 'MCH-001', 'Macchina 1', 'CNC',   'Running', 'system', @Now, 'system', @Now),
(@MachineId2, @LineId, 'MCH-002', 'Macchina 2', 'Robot', 'Running', 'system', @Now, 'system', @Now),
(@MachineId3, @LineId, 'MCH-003', 'Macchina 3', 'CNC',   'Running', 'system', @Now, 'system', @Now),
(@MachineId4, @LineId, 'MCH-004', 'Macchina 4', 'Robot', 'Running', 'system', @Now, 'system', @Now);

-- ==============================================================
-- 5. ProductionOrder (1 ordine sulla linea) -da qui in poi i dati di test
--- nuovo ordine sulle macchine per oggi
-- ==============================================================
DECLARE @OrderId UNIQUEIDENTIFIER = NEWID();
INSERT INTO ProductionOrders (Id, OrderNumber, ProductCode, PlannedQuantity, StartTime, EndTime, ProductionLineId, UserCreate, CreateDate, UserModify, LastModifiedDate)
VALUES (@OrderId, 'ORD-00001', 'PRD-001', 500, @StartTime, @StopTime, @LineId, 'system', @Now, 'system', @Now);

-- ==============================================================
-- 6. MachineEvent — START e STOP per ogni macchina
-- ==============================================================
INSERT INTO MachineEvent (Id, MachineId, EventType, EventTime, Message, UserCreate, CreateDate, UserModify, LastModifiedDate)
VALUES
-- Macchina 1
(NEWID(), @MachineId1, 'START', @StartTime,                        'Avvio turno mattina', 'system', @Now, 'system', @Now),
(NEWID(), @MachineId1, 'STOP',  @StopTime,                         'Fine turno mattina',  'system', @Now, 'system', @Now),
-- Macchina 2
(NEWID(), @MachineId2, 'START', @StartTime,                        'Avvio turno mattina', 'system', @Now, 'system', @Now),
(NEWID(), @MachineId2, 'STOP',  @StopTime,                         'Fine turno mattina',  'system', @Now, 'system', @Now),
-- Macchina 3
(NEWID(), @MachineId3, 'START', @StartTime,                        'Avvio turno mattina', 'system', @Now, 'system', @Now),
(NEWID(), @MachineId3, 'STOP',  @StopTime,                         'Fine turno mattina',  'system', @Now, 'system', @Now),
-- Macchina 4
(NEWID(), @MachineId4, 'START', @StartTime,                        'Avvio turno mattina', 'system', @Now, 'system', @Now),
(NEWID(), @MachineId4, 'STOP',  @StopTime,                         'Fine turno mattina',  'system', @Now, 'system', @Now);

-- ==============================================================
-- 7. ProductionData — ogni 30 minuti tra START e STOP
--    15 record per macchina (08:30 → 16:00)
-- ==============================================================
DECLARE @machines TABLE (Id UNIQUEIDENTIFIER);
INSERT INTO @machines VALUES (@MachineId1), (@MachineId2), (@MachineId3), (@MachineId4);

DECLARE @MId UNIQUEIDENTIFIER;
DECLARE mc CURSOR FOR SELECT Id FROM @machines;
OPEN mc;
FETCH NEXT FROM mc INTO @MId;

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
            -- quantità prodotta: base 80-120 pezzi con leggera variazione
            80 + (ABS(CHECKSUM(NEWID())) % 41),
            -- scarti: 0-5 pezzi
            ABS(CHECKSUM(NEWID())) % 6,
            -- cycle time: 1.5 - 3.5 secondi
            1.5 + (ABS(CHECKSUM(NEWID())) % 20) / 10.0,
            -- energia: 80 - 120 kWh
            80.0 + (ABS(CHECKSUM(NEWID())) % 41),
            -- temperatura: 35 - 55 gradi
            35.0 + (ABS(CHECKSUM(NEWID())) % 21),
            'system', @Now, 'system', @Now
        );

        SET @slot += 1;
    END;

    FETCH NEXT FROM mc INTO @MId;
END;
CLOSE mc;
DEALLOCATE mc;

-- ==============================================================
PRINT '✅ Seed completato:';
PRINT '   - 1 linea di produzione (Linea A)';
PRINT '   - 4 macchine (MCH-001 / 002 / 003 / 004)';
PRINT '   - 2 eventi per macchina (START 08:00 / STOP 16:00)';
PRINT '   - 15 record di produzione per macchina (08:30 → 16:00, ogni 30 min)';
PRINT '   - 60 record totali ProductionData';
