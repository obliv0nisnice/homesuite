using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpawnRecurringTemplateInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Wiederkehrende Transaktionen sind ab jetzt reine Vorlagen und zählen
            // nicht mehr als Buchung. Für jede bestehende Vorlage wird ihre bisherige
            // "Erst-Buchung" als eigenständige Transaktion nachgezogen, damit in der
            // Monatsansicht nichts verschwindet.
            migrationBuilder.Sql("""
                INSERT INTO "Transactions" ("Id", "Title", "Amount", "Date", "Note", "CategoryId", "IsRecurring", "RecurringInterval", "NextDueDate")
                SELECT gen_random_uuid(), t."Title", t."Amount", t."Date", t."Note", t."CategoryId", FALSE, NULL, NULL
                FROM "Transactions" t
                WHERE t."IsRecurring" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Datenmigration ohne Schemaänderung — kein automatisches Rollback der
            // erzeugten Buchungen (sie wären von Hand angelegten nicht unterscheidbar).
        }
    }
}
