/*
  HU-38 Mejoras del formulario de registro: celular del paciente.
  Ejecutar una sola vez sobre GestionCitasDb ANTES de levantar el backend de esta rama.
  Sin esta columna, el login y el registro fallan ("Invalid column name 'Phone'").

  El script es idempotente: si la columna ya existe, no hace nada.
  No modifica ni elimina datos: las cuentas existentes quedan con Phone = NULL.
*/

USE [GestionCitasDb];
GO

IF COL_LENGTH('dbo.Users', 'Phone') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD Phone NVARCHAR(15) NULL;
END;
GO

SELECT CASE WHEN COL_LENGTH('dbo.Users', 'Phone') IS NOT NULL THEN 'Users.Phone = OK' ELSE 'Users.Phone = FALTA' END AS Resultado;
GO
