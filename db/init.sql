-- Базовая схема БД социальной сети PulseNet (ДЗ №1 OTUS Highload Architect)
-- Согласно ТЗ: без репликации, шардирования, кэширования и дополнительных вторичных индексов.

CREATE TABLE IF NOT EXISTS users (
    id            UUID PRIMARY KEY,
    first_name    VARCHAR(100) NOT NULL CHECK (char_length(btrim(first_name)) >= 1),
    second_name   VARCHAR(100) NOT NULL CHECK (char_length(btrim(second_name)) >= 1),
    birthdate     DATE NOT NULL CHECK (birthdate >= DATE '1900-01-01' AND birthdate <= CURRENT_DATE),
    gender        VARCHAR(16) NOT NULL DEFAULT 'unspecified' CHECK (gender IN ('male', 'female', 'unspecified')),
    biography     TEXT NOT NULL CHECK (char_length(btrim(biography)) >= 1 AND char_length(biography) <= 2000),
    city          VARCHAR(100) NOT NULL CHECK (char_length(btrim(city)) >= 1),
    password_hash VARCHAR(256) NOT NULL CHECK (password_hash LIKE 'pbkdf2_sha256$%'),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
