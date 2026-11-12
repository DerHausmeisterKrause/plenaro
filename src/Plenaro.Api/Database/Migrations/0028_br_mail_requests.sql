-- One submission per user and day. Never auto-resend a message whose
-- Graph response or local persistence is ambiguous.
CREATE TABLE br_mail_requests (
    user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    day date NOT NULL,
    status varchar(16) NOT NULL DEFAULT 'processing'
        CHECK(status IN ('processing','accepted','unknown')),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(user_id,day)
);
