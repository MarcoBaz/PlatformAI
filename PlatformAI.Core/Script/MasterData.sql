DECLARE @Countries TABLE (Code NVARCHAR(10), Description NVARCHAR(200), Id UNIQUEIDENTIFIER);

INSERT INTO Countries (Id, Code, Description)
OUTPUT INSERTED.Code, INSERTED.Description, INSERTED.Id INTO @Countries
VALUES
(NEWID(), 'IT', 'Italia'),
(NEWID(), 'US', 'Stati Uniti'),
(NEWID(), 'DE', 'Germania'),
(NEWID(), 'FR', 'Francia'),
(NEWID(), 'UK', 'Regno Unito'),
(NEWID(), 'ES', 'Spagna'),
(NEWID(), 'CA', 'Canada'),
(NEWID(), 'AU', 'Australia'),
(NEWID(), 'JP', 'Giappone'),
(NEWID(), 'CN', 'Cina'),
(NEWID(), 'BR', 'Brasile'),
(NEWID(), 'IN', 'India'),
(NEWID(), 'RU', 'Russia'),
(NEWID(), 'MX', 'Messico'),
(NEWID(), 'CH', 'Svizzera'),
(NEWID(), 'AR', 'Argentina'),
(NEWID(), 'SE', 'Svezia'),
(NEWID(), 'NO', 'Norvegia'),
(NEWID(), 'DK', 'Danimarca'),
(NEWID(), 'AT', 'Austria'),
(NEWID(), 'BE', 'Belgio'),
(NEWID(), 'NL', 'Olanda'),
(NEWID(), 'PT', 'Portogallo'),
(NEWID(), 'GR', 'Grecia'),
(NEWID(), 'TR', 'Turchia'),
(NEWID(), 'PL', 'Polonia'),
(NEWID(), 'FI', 'Finlandia'),
(NEWID(), 'IE', 'Irlanda'),
(NEWID(), 'KR', 'Corea del Sud'),
(NEWID(), 'ZA', 'Sud Africa'),
(NEWID(), 'NZ', 'Nuova Zelanda'),
(NEWID(), 'CL', 'Cile'),
(NEWID(), 'CO', 'Colombia'),
(NEWID(), 'SG', 'Singapore'),
(NEWID(), 'TH', 'Thailandia'),
(NEWID(), 'ID', 'Indonesia'),
(NEWID(), 'PH', 'Filippine'),
(NEWID(), 'MY', 'Malaysia'),
(NEWID(), 'VN', 'Vietnam'),
(NEWID(), 'EG', 'Egitto'),
(NEWID(), 'MA', 'Marocco'),
(NEWID(), 'DZ', 'Algeria'),
(NEWID(), 'KE', 'Kenya'),
(NEWID(), 'NG', 'Nigeria'),
(NEWID(), 'GH', 'Ghana'),
(NEWID(), 'CI', 'Costa d''Avorio'),
(NEWID(), 'AO', 'Angola'),
(NEWID(), 'TZ', 'Tanzania'),
(NEWID(), 'UG', 'Uganda'),
(NEWID(), 'ET', 'Etiopia'),
(NEWID(), 'RW', 'Ruanda'),
(NEWID(), 'BI', 'Burundi'),
(NEWID(), 'MG', 'Madagascar'),
(NEWID(), 'MU', 'Mauritius'),
(NEWID(), 'JM', 'Giamaica'),
(NEWID(), 'TT', 'Trinidad e Tobago'),
(NEWID(), 'BB', 'Barbados'),
(NEWID(), 'BS', 'Bahamas'),
(NEWID(), 'GY', 'Guyana'),
(NEWID(), 'SR', 'Suriname'),
(NEWID(), 'BO', 'Bolivia'),
(NEWID(), 'PE', 'Perù'),
(NEWID(), 'VE', 'Venezuela'),
(NEWID(), 'EC', 'Ecuador'),
(NEWID(), 'UY', 'Uruguay'),
(NEWID(), 'PY', 'Paraguay'),
(NEWID(), 'CU', 'Cuba'),
(NEWID(), 'HT', 'Haiti'),
(NEWID(), 'DO', 'Repubblica Dominicana'),
(NEWID(), 'GT', 'Guatemala'),
(NEWID(), 'HN', 'Honduras'),
(NEWID(), 'SV', 'El Salvador'),
(NEWID(), 'NI', 'Nicaragua'),
(NEWID(), 'PA', 'Panama'),
(NEWID(), 'CR', 'Costa Rica'),
(NEWID(), 'BZ', 'Belize');

DECLARE @Italy UNIQUEIDENTIFIER =
(
    SELECT Id FROM @Countries WHERE Code = 'IT'
);

DECLARE @UserFunctions TABLE (Code NVARCHAR(10), Id UNIQUEIDENTIFIER);

INSERT INTO UserFunction (Id, Code, Description)
OUTPUT INSERTED.Code, INSERTED.Id INTO @UserFunctions
VALUES
(NEWID(), '001', 'System Administrator'),
(NEWID(), '002', 'Tenant Administrator'),
(NEWID(), '003', 'Web Api Consumer'),
(NEWID(), '004', 'User');

DECLARE @UserRoles TABLE (Code NVARCHAR(10), Id UNIQUEIDENTIFIER);

INSERT INTO UserRole (Id, Code, Description)
OUTPUT INSERTED.Code, INSERTED.Id INTO @UserRoles
VALUES
(NEWID(), 'SA', 'System Administrator');



INSERT INTO UserRoleFunctionTuple (UserFunctionId, UserRoleId)
SELECT
    uf.Id,
    ur.Id
FROM @UserFunctions uf
CROSS JOIN @UserRoles ur
WHERE uf.Code = '001'
  AND ur.Code = 'SA';


INSERT INTO SettingKey ([Key])
VALUES
('DatabaseConnection');


-- Inserimento user Qgs Per consumer su Web API


INSERT INTO Tenants (Code, Name, Description, Street,City,Province,CountryId,UserCreate,CreateDate,UserModify,LastModifiedDate,ValidityDate)VALUES
('TENANT001','Test Tenant','Test Tenant','Via prova 4','Nogaredo','TN',@Italy,'Marco Bazzoli', GETDATE(), 'Marco Bazzoli', GETDATE(),null);

DECLARE @TenantId UNIQUEIDENTIFIER =
(
    SELECT Id FROM Tenants WHERE Code = 'TENANT001'
);

DECLARE @RoleId UNIQUEIDENTIFIER =
(
    SELECT Id FROM UserRole WHERE Code = 'SA'
);

INSERT INTO [Users] (Name, Surname,Login,Password,Email,BearerToken,Enabled,TenantId, LanguageCode,RoleId,UserCreate,CreateDate,UserModify,LastModifiedDate,ValidityDate)VALUES
('Test','User','TestUser','TestUser','test@user.it','fb6dd2c5-0bfd-4178-b1f5-ee4c76c328a9',1,@TenantId,'IT',@RoleId,'Marco Bazzoli', GETDATE(), 'Marco Bazzoli', GETDATE(),null);
