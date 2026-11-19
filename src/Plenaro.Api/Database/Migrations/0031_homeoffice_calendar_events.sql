-- Outlook event side effects are separately tracked from one-time mail sending.
-- A failed/unknown Graph POST is NEVER blindly repeated: the next explicit
-- request first searches for the exact Plenaro/WPF signature.
CREATE TABLE homeoffice_calendar_events (
    user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    day date NOT NULL,
    status varchar(16) NOT NULL
        CHECK(status IN ('processing','created','unknown','removed')),
    event_id varchar(512),
    transaction_id uuid NOT NULL DEFAULT gen_random_uuid(),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(user_id,day)
);
