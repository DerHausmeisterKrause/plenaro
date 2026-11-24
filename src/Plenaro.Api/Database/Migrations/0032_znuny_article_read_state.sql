-- Per-user WPF-style article read state. No Znuny write access required.
-- The first reliable article snapshot establishes a silent read baseline.
-- Later articles become unread; reading one article clears only that article.
CREATE TABLE znuny_ticket_article_heads (
    ticket_id varchar(80) PRIMARY KEY REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
    newest_article_id varchar(80) NOT NULL,
    newest_created_at timestamptz,
    checked_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE znuny_ticket_article_watch (
    user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    ticket_id varchar(80) NOT NULL REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
    newest_article_id varchar(80) NOT NULL,
    newest_created_at timestamptz,
    initialized_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(user_id,ticket_id)
);
CREATE TABLE znuny_ticket_article_read_state (
    user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    ticket_id varchar(80) NOT NULL REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
    article_id varchar(80) NOT NULL,
    first_seen_at timestamptz NOT NULL DEFAULT now(),
    read_at timestamptz,
    PRIMARY KEY(user_id,ticket_id,article_id)
);
CREATE INDEX ix_znuny_article_unread_user ON znuny_ticket_article_read_state(user_id,ticket_id)
    WHERE read_at IS NULL;
