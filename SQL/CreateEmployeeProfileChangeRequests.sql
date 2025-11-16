-- Create EmployeeProfileChangeRequests table for ESS Leave System
-- This table stores employee profile change requests that require manager approval

CREATE TABLE IF NOT EXISTS "EmployeeProfileChangeRequests" (
    "RequestId" INTEGER NOT NULL CONSTRAINT "PK_EmployeeProfileChangeRequests" PRIMARY KEY AUTOINCREMENT,
    "EmployeeId" INTEGER NOT NULL,
    "ManagerId" INTEGER NULL,
    "NewValuesJson" TEXT NOT NULL,
    "OriginalValuesJson" TEXT NULL,
    "Status" INTEGER NOT NULL DEFAULT 0,
    "RequestedAt" TEXT NOT NULL,
    "ReviewedAt" TEXT NULL,
    "ManagerComment" TEXT NULL
);

-- Add index for better query performance
CREATE INDEX IF NOT EXISTS "IX_EmployeeProfileChangeRequests_EmployeeId" ON "EmployeeProfileChangeRequests" ("EmployeeId");
CREATE INDEX IF NOT EXISTS "IX_EmployeeProfileChangeRequests_ManagerId" ON "EmployeeProfileChangeRequests" ("ManagerId");
CREATE INDEX IF NOT EXISTS "IX_EmployeeProfileChangeRequests_Status" ON "EmployeeProfileChangeRequests" ("Status");