CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;

CREATE TABLE "AspNetRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
    "Name" TEXT NULL,
    "NormalizedName" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL
);

CREATE TABLE "AspNetUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
    "UserName" TEXT NULL,
    "NormalizedUserName" TEXT NULL,
    "Email" TEXT NULL,
    "NormalizedEmail" TEXT NULL,
    "EmailConfirmed" INTEGER NOT NULL,
    "PasswordHash" TEXT NULL,
    "SecurityStamp" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "PhoneNumberConfirmed" INTEGER NOT NULL,
    "TwoFactorEnabled" INTEGER NOT NULL,
    "LockoutEnd" TEXT NULL,
    "LockoutEnabled" INTEGER NOT NULL,
    "AccessFailedCount" INTEGER NOT NULL
);

CREATE TABLE "LeaveApplications" (
    "LeaveApplicationId" INTEGER NOT NULL CONSTRAINT "PK_LeaveApplications" PRIMARY KEY AUTOINCREMENT,
    "EmployeeId" INTEGER NOT NULL,
    "LeaveTypeId" INTEGER NOT NULL,
    "StartDate" TEXT NOT NULL,
    "EndDate" TEXT NOT NULL,
    "TotalDays" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "AppliedDate" TEXT NOT NULL,
    "ReviewedById" INTEGER NULL,
    "ReviewedDate" TEXT NULL,
    "ReviewComments" TEXT NULL,
    "ContactDuringLeave" TEXT NULL,
    "SupportingDocumentPath" TEXT NULL
);

CREATE TABLE "LeaveBalances" (
    "LeaveBalanceId" INTEGER NOT NULL CONSTRAINT "PK_LeaveBalances" PRIMARY KEY AUTOINCREMENT,
    "EmployeeId" INTEGER NOT NULL,
    "LeaveTypeId" INTEGER NOT NULL,
    "Year" INTEGER NOT NULL,
    "TotalDays" TEXT NOT NULL,
    "UsedDays" TEXT NOT NULL,
    "PendingDays" TEXT NOT NULL,
    "CarryForwardDays" TEXT NOT NULL,
    "CreatedDate" TEXT NOT NULL,
    "LastModifiedDate" TEXT NULL
);

CREATE TABLE "LeaveTypes" (
    "LeaveTypeId" INTEGER NOT NULL CONSTRAINT "PK_LeaveTypes" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "DefaultDaysPerYear" INTEGER NOT NULL,
    "RequiresApproval" INTEGER NOT NULL,
    "IsPaid" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "Color" TEXT NULL
);

CREATE TABLE "Notifications" (
    "NotificationId" INTEGER NOT NULL CONSTRAINT "PK_Notifications" PRIMARY KEY AUTOINCREMENT,
    "EmployeeId" INTEGER NOT NULL,
    "Title" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "ActionUrl" TEXT NULL,
    "NotificationType" INTEGER NOT NULL,
    "IsRead" INTEGER NOT NULL,
    "CreatedDate" TEXT NOT NULL,
    "ReadDate" TEXT NULL
);

CREATE TABLE "AspNetRoleClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
    "RoleId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserLogins" (
    "LoginProvider" TEXT NOT NULL,
    "ProviderKey" TEXT NOT NULL,
    "ProviderDisplayName" TEXT NULL,
    "UserId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserRoles" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AspNetUserTokens" (
    "UserId" TEXT NOT NULL,
    "LoginProvider" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Value" TEXT NULL,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");

CREATE UNIQUE INDEX "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");

CREATE INDEX "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");

CREATE INDEX "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");

CREATE INDEX "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");

CREATE INDEX "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");

CREATE UNIQUE INDEX "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");

CREATE INDEX "IX_LeaveApplications_AppliedDate" ON "LeaveApplications" ("AppliedDate");

CREATE INDEX "IX_LeaveApplications_StartDate" ON "LeaveApplications" ("StartDate");

CREATE INDEX "IX_LeaveApplications_Status" ON "LeaveApplications" ("Status");

CREATE UNIQUE INDEX "IX_LeaveBalances_EmployeeId_LeaveTypeId_Year" ON "LeaveBalances" ("EmployeeId", "LeaveTypeId", "Year");

CREATE UNIQUE INDEX "IX_LeaveTypes_Name" ON "LeaveTypes" ("Name");

CREATE INDEX "IX_Notifications_CreatedDate" ON "Notifications" ("CreatedDate");

CREATE INDEX "IX_Notifications_EmployeeId_IsRead" ON "Notifications" ("EmployeeId", "IsRead");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251016164927_AddNotificationsSystem', '8.0.10');

COMMIT;

