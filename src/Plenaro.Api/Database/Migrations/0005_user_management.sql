-- Distinguish administrator-disabled accounts from automatic temporary login lockouts.
ALTER TABLE "AspNetUsers" ADD COLUMN "IsDisabled" boolean NOT NULL DEFAULT false;
