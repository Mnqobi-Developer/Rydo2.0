using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rydo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $rydo$
                DECLARE
                    table_name text;
                BEGIN
                    FOREACH table_name IN ARRAY ARRAY[
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
                        'dispute_messages'
                    ]
                    LOOP
                        EXECUTE format(
                            'ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY',
                            table_name);
                    END LOOP;
                END
                $rydo$;

                CREATE SCHEMA IF NOT EXISTS private;
                REVOKE ALL ON SCHEMA private FROM PUBLIC;

                CREATE OR REPLACE FUNCTION private.rydo_enable_rls_for_public_tables()
                RETURNS event_trigger
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog
                AS $rydo$
                DECLARE
                    command record;
                BEGIN
                    FOR command IN
                        SELECT *
                        FROM pg_event_trigger_ddl_commands()
                        WHERE command_tag IN (
                            'CREATE TABLE',
                            'CREATE TABLE AS',
                            'SELECT INTO')
                          AND object_type IN ('table', 'partitioned table')
                    LOOP
                        IF command.schema_name = 'public' THEN
                            EXECUTE format(
                                'ALTER TABLE IF EXISTS %s ENABLE ROW LEVEL SECURITY',
                                command.object_identity);
                        END IF;
                    END LOOP;
                END
                $rydo$;

                DROP EVENT TRIGGER IF EXISTS rydo_enable_rls_on_public_table;

                CREATE EVENT TRIGGER rydo_enable_rls_on_public_table
                ON ddl_command_end
                WHEN TAG IN ('CREATE TABLE', 'CREATE TABLE AS', 'SELECT INTO')
                EXECUTE FUNCTION private.rydo_enable_rls_for_public_tables();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP EVENT TRIGGER IF EXISTS rydo_enable_rls_on_public_table;
                DROP FUNCTION IF EXISTS private.rydo_enable_rls_for_public_tables();

                DO $rydo$
                DECLARE
                    table_name text;
                BEGIN
                    FOREACH table_name IN ARRAY ARRAY[
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
                        'dispute_messages'
                    ]
                    LOOP
                        EXECUTE format(
                            'ALTER TABLE public.%I DISABLE ROW LEVEL SECURITY',
                            table_name);
                    END LOOP;
                END
                $rydo$;
                """);
        }
    }
}
