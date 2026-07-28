\set ON_ERROR_STOP on

DO $rydo$
DECLARE
    unprotected_tables text;
BEGIN
    WITH expected(table_name) AS (
        VALUES
            ('__EFMigrationsHistory'),
            ('users'),
            ('admin_credentials'),
            ('admin_audit_logs'),
            ('otp_challenges'),
            ('auth_sessions'),
            ('session_refresh_tokens'),
            ('passenger_profiles'),
            ('driver_profiles'),
            ('driver_documents'),
            ('driver_vehicles'),
            ('trips'),
            ('driver_availability'),
            ('trip_offers'),
            ('payments'),
            ('payment_events'),
            ('ratings'),
            ('disputes'),
            ('dispute_messages')
    )
    SELECT string_agg(expected.table_name, ', ' ORDER BY expected.table_name)
    INTO unprotected_tables
    FROM expected
    LEFT JOIN pg_catalog.pg_class AS relation
        ON relation.relname = expected.table_name
       AND relation.relnamespace = 'public'::regnamespace
    WHERE relation.oid IS NULL OR NOT relation.relrowsecurity;

    IF unprotected_tables IS NOT NULL THEN
        RAISE EXCEPTION 'RLS is missing from: %', unprotected_tables;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_catalog.pg_policies
        WHERE schemaname = 'public'
          AND tablename IN (
              '__EFMigrationsHistory',
              'users',
              'admin_credentials',
              'admin_audit_logs',
              'otp_challenges',
              'auth_sessions',
              'session_refresh_tokens',
              'passenger_profiles',
              'driver_profiles',
              'driver_documents',
              'driver_vehicles',
              'trips',
              'driver_availability',
              'trip_offers',
              'payments',
              'payment_events',
              'ratings',
              'disputes',
              'dispute_messages'))
    THEN
        RAISE EXCEPTION 'Backend-only tables must not expose client RLS policies.';
    END IF;
END
$rydo$;

CREATE TABLE public.rydo_rls_future_table_probe (id integer PRIMARY KEY);

DO $rydo$
BEGIN
    IF NOT (
        SELECT relation.relrowsecurity
        FROM pg_catalog.pg_class AS relation
        WHERE relation.oid = 'public.rydo_rls_future_table_probe'::regclass)
    THEN
        RAISE EXCEPTION 'The automatic RLS event trigger did not protect a new public table.';
    END IF;
END
$rydo$;

DROP TABLE public.rydo_rls_future_table_probe;
