-- Safe, shared text search index. Populated only by trusted Znuny TicketGet
-- articles; never from arbitrary browser input. Ticket access is still enforced
-- against approved agent mappings for EVERY search query.
CREATE TABLE znuny_ticket_search_documents (
  ticket_id varchar(80) PRIMARY KEY REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
  content text NOT NULL DEFAULT '',
  indexed_changed_at timestamptz,
  indexed_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_znuny_ticket_search_fts ON znuny_ticket_search_documents
  USING GIN(to_tsvector('simple',content));
