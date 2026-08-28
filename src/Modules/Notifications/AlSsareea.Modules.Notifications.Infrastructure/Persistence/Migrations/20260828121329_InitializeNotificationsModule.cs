using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // EF-generated migration arguments are immutable metadata

namespace AlSsareea.Modules.Notifications.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitializeNotificationsModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "notifications");

        migrationBuilder.CreateTable(
            name: "notification_audit",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                entity_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_audit", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "notification_device_tokens",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_ciphertext = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                token_mask = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                platform = table.Column<short>(type: "smallint", nullable: false),
                provider = table.Column<short>(type: "smallint", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deactivated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deactivation_reason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_device_tokens", x => x.id);
                table.CheckConstraint("ck_notification_device_tokens_platform", "platform BETWEEN 1 AND 3");
                table.CheckConstraint("ck_notification_device_tokens_provider", "provider BETWEEN 1 AND 2");
            });

        migrationBuilder.CreateTable(
            name: "notification_inbox_messages",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_inbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "notification_outbox_messages",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                error_code = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_outbox_messages", x => x.id);
                table.CheckConstraint("ck_notification_outbox_attempts", "attempt_count >= 0");
                table.CheckConstraint("ck_notification_outbox_payload", "jsonb_typeof(payload) = 'object'");
            });

        migrationBuilder.CreateTable(
            name: "notification_preferences",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                channel = table.Column<short>(type: "smallint", nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_preferences", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "notification_templates",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                channel = table.Column<short>(type: "smallint", nullable: false),
                language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_templates", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "notifications",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                template_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                channel = table.Column<short>(type: "smallint", nullable: false),
                language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                read_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notifications", x => x.id);
                table.CheckConstraint("ck_notifications_body", "char_length(body) > 0");
                table.CheckConstraint("ck_notifications_channel", "channel BETWEEN 1 AND 5");
                table.CheckConstraint("ck_notifications_status", "status BETWEEN 1 AND 7");
            });

        migrationBuilder.CreateTable(
            name: "notification_deliveries",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                status = table.Column<short>(type: "smallint", nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                maximum_attempts = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                last_error_code = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_deliveries", x => x.id);
                table.CheckConstraint("ck_notification_deliveries_attempts", "attempt_count >= 0 AND attempt_count <= maximum_attempts");
                table.CheckConstraint("ck_notification_deliveries_status", "status BETWEEN 1 AND 7");
                table.ForeignKey(
                    name: "fk_notification_deliveries_notifications_notification_id",
                    column: x => x.notification_id,
                    principalSchema: "notifications",
                    principalTable: "notifications",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "notification_attempts",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                notification_delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                attempt_number = table.Column<int>(type: "integer", nullable: false),
                succeeded = table.Column<bool>(type: "boolean", nullable: false),
                failure_kind = table.Column<short>(type: "smallint", nullable: false),
                error_code = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                provider_message_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                attempted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_attempts", x => x.id);
                table.ForeignKey(
                    name: "fk_notification_attempts_notification_deliveries_notification_",
                    column: x => x.notification_delivery_id,
                    principalSchema: "notifications",
                    principalTable: "notification_deliveries",
                    principalColumn: "id");
            });

        migrationBuilder.InsertData(
            schema: "notifications",
            table: "notification_templates",
            columns: new[] { "id", "body", "channel", "created_at_utc", "is_active", "key", "language", "subject", "updated_at_utc" },
            values: new object[,]
            {
                { new Guid("0c7992c4-afe1-2ffd-244f-d97dd839709d"), "עדכון משלוח להזמנה {{orderId}}", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "delivery.status.customer", "he", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("37ec47bd-a66e-d796-11ff-db75a1e9e59b"), "ההזמנה שלך {{orderNumber}} התקבלה", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.customer", "he", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("46898ce7-2c97-2f10-3061-228aae724339"), "طلب جديد {{orderNumber}}", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.merchant", "ar", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("490faf13-beba-4937-f1af-7c4224c251d7"), "Your order {{orderNumber}} was received", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.customer", "en", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("562a4ec8-26d5-3b7e-cfd5-242523f7c508"), "You have a new delivery offer", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "dispatch.offer.driver", "en", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("574f7d2c-4db0-6c53-2bb5-6a32c2eac25f"), "יש לך הצעת משלוח חדשה", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "dispatch.offer.driver", "he", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("6fa6c8b3-df88-15e5-9c5d-e10d81c25c96"), "You have a new delivery offer", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "dispatch.offer.driver", "en", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("716eea16-4642-a5d8-54d2-c9aa7049950e"), "הזמנה חדשה {{orderNumber}}", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.merchant", "he", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("834732b2-f6df-1bb0-d262-c140a23b7d57"), "Delivery update for order {{orderId}}", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "delivery.status.customer", "en", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("89c83315-8089-20e9-9370-dbfd4e91e741"), "تحديث التوصيل للطلب {{orderId}}", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "delivery.status.customer", "ar", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("990934b4-6440-5da5-c07f-b128d487dc74"), "New order {{orderNumber}}", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.merchant", "en", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("acc212c4-1350-d3b3-b2cf-cdbfcceb9044"), "יש לך הצעת משלוח חדשה", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "dispatch.offer.driver", "he", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("bcfbfb54-6826-44f8-aa2d-92312c249bbc"), "Your order {{orderNumber}} was received", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.customer", "en", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("c045063d-1e40-3afb-1b22-489b3b26818f"), "تم استلام طلبك {{orderNumber}}", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.customer", "ar", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("c4e35bb4-cca1-1f82-473c-4527935b276a"), "עדכון משלוח להזמנה {{orderId}}", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "delivery.status.customer", "he", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("e11cbb2e-95fc-bc24-8126-ffeeccbaa519"), "New order {{orderNumber}}", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.merchant", "en", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("e31c4320-95b7-81f9-4a5e-8d3c2ea9dd49"), "تم استلام طلبك {{orderNumber}}", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.customer", "ar", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("ebe72aff-0d49-b900-75ff-a38ac743c199"), "הזמנה חדשה {{orderNumber}}", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.merchant", "he", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("ec96469e-5266-37b5-7d46-59d08b007f54"), "Delivery update for order {{orderId}}", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "delivery.status.customer", "en", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("edfcd08b-7c97-2238-b208-d38916cd01a5"), "طلب جديد {{orderNumber}}", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.merchant", "ar", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("f50a4478-2f9d-04d3-bfcf-0577f0e56b87"), "لديك عرض توصيل جديد", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "dispatch.offer.driver", "ar", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("f7195098-80e4-1a4b-bf03-1a0dc6d6dc32"), "ההזמנה שלך {{orderNumber}} התקבלה", (short)4, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "order.created.customer", "he", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("ff07757b-32be-afe8-a16d-5abed9b518e7"), "تحديث التوصيل للطلب {{orderId}}", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "delivery.status.customer", "ar", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("ff4cd443-7adc-daeb-1e4d-38afa68493f5"), "لديك عرض توصيل جديد", (short)1, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc), true, "dispatch.offer.driver", "ar", null, new DateTime(2026, 8, 28, 0, 0, 0, 0, DateTimeKind.Utc) }
            });

        migrationBuilder.CreateIndex(
            name: "ix_notification_attempts_notification_delivery_id_attempt_numb",
            schema: "notifications",
            table: "notification_attempts",
            columns: new[] { "notification_delivery_id", "attempt_number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_notification_audit_user_id_occurred_at_utc",
            schema: "notifications",
            table: "notification_audit",
            columns: new[] { "user_id", "occurred_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ix_notification_deliveries_notification_id",
            schema: "notifications",
            table: "notification_deliveries",
            column: "notification_id");

        migrationBuilder.CreateIndex(
            name: "ix_notification_deliveries_status_next_attempt_at_utc",
            schema: "notifications",
            table: "notification_deliveries",
            columns: new[] { "status", "next_attempt_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ix_notification_device_tokens_token_hash",
            schema: "notifications",
            table: "notification_device_tokens",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_notification_device_tokens_user_id_is_active",
            schema: "notifications",
            table: "notification_device_tokens",
            columns: new[] { "user_id", "is_active" });

        migrationBuilder.CreateIndex(
            name: "ix_notification_inbox_messages_processed_at_utc",
            schema: "notifications",
            table: "notification_inbox_messages",
            column: "processed_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_notification_outbox_messages_processed_at_utc_occurred_at_u",
            schema: "notifications",
            table: "notification_outbox_messages",
            columns: new[] { "processed_at_utc", "occurred_at_utc" },
            filter: "processed_at_utc IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_notification_preferences_user_id_category_channel",
            schema: "notifications",
            table: "notification_preferences",
            columns: new[] { "user_id", "category", "channel" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_notification_templates_key_channel_language",
            schema: "notifications",
            table: "notification_templates",
            columns: new[] { "key", "channel", "language" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_notifications_source_event_id_user_id_channel",
            schema: "notifications",
            table: "notifications",
            columns: new[] { "source_event_id", "user_id", "channel" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_notifications_user_id_created_at_utc",
            schema: "notifications",
            table: "notifications",
            columns: new[] { "user_id", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "ix_notifications_user_id_read_at_utc",
            schema: "notifications",
            table: "notifications",
            columns: new[] { "user_id", "read_at_utc" },
            filter: "channel = 4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notification_attempts",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "notification_audit",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "notification_device_tokens",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "notification_inbox_messages",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "notification_outbox_messages",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "notification_preferences",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "notification_templates",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "notification_deliveries",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "notifications",
            schema: "notifications");
    }
}
