-- ================================================================
-- CNU Facebook API - PostgreSQL Database Setup Script
-- ================================================================

CREATE TABLE IF NOT EXISTS accesstokenfacebook (
    id              BIGSERIAL       PRIMARY KEY,
    accesstoken     VARCHAR(512)    NOT NULL,
    longlivetoken   VARCHAR(512),
    pageid          VARCHAR(50)     NOT NULL,
    pagename        VARCHAR(255),
    openstatus      VARCHAR(1)      DEFAULT '1',
    createuserid    VARCHAR(100)    NOT NULL,
    createdate      DATE,
    createtime      VARCHAR(8),
    updateuserid    VARCHAR(100),
    updatedate      DATE,
    updatetime      VARCHAR(8),

    CONSTRAINT uq_page_user UNIQUE (pageid, createuserid)
);

CREATE INDEX IF NOT EXISTS ix_accesstokenfacebook_pageid ON accesstokenfacebook (pageid);
CREATE INDEX IF NOT EXISTS ix_accesstokenfacebook_createuserid ON accesstokenfacebook (createuserid);
