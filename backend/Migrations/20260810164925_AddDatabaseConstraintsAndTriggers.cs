using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreightLink.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseConstraintsAndTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgencyStaff_Users_UserId",
                table: "AgencyStaff");

            migrationBuilder.DropForeignKey(
                name: "FK_AgencyStatusHistories_Agencies_AgencyId",
                table: "AgencyStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_AgentWorkflowRuns_WorkflowRunId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentResponses_Assignments_AssignmentId",
                table: "AssignmentResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_ComplianceDocs_Agencies_AgencyId",
                table: "ComplianceDocs");

            migrationBuilder.DropForeignKey(
                name: "FK_DisputeResolutions_Disputes_DisputeId",
                table: "DisputeResolutions");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Loads_LoadId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_LoadStatusHistories_Loads_LoadId",
                table: "LoadStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipperProfiles_Users_UserId",
                table: "ShipperProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_TripEvents_Trips_TripId",
                table: "TripEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_TripEvidences_Trips_TripId",
                table: "TripEvidences");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_AgencyId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_RegistrationNo",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Trips_DriverId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_VehicleId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_TripEvidences_TripId",
                table: "TripEvidences");

            migrationBuilder.DropIndex(
                name: "IX_ToolCalls_AgentStepId",
                table: "ToolCalls");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_MatchCandidates_WorkflowRunId",
                table: "MatchCandidates");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_TripId",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "IX_ComplianceDocs_AgencyId",
                table: "ComplianceDocs");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_LoadId",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalDecisions_WorkflowRunId",
                table: "ApprovalDecisions");

            migrationBuilder.DropIndex(
                name: "IX_AgentWorkflowRuns_LoadId",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_AgentSteps_WorkflowRunId",
                table: "AgentSteps");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                table: "Users",
                newName: "uq_user_email");

            migrationBuilder.RenameIndex(
                name: "IX_Loads_ReferenceCode",
                table: "Loads",
                newName: "uq_load_reference");

            migrationBuilder.RenameIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                newName: "uq_invoice_number");

            migrationBuilder.RenameIndex(
                name: "IX_Agencies_BusinessRegNo",
                table: "Agencies",
                newName: "uq_agency_regno");

            migrationBuilder.CreateIndex(
                name: "uq_vehicle_agency_regno",
                table: "Vehicles",
                columns: new[] { "AgencyId", "RegistrationNo" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_capacity",
                table: "Vehicles",
                sql: "\"CapacityKg\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicle_volume",
                table: "Vehicles",
                sql: "\"VolumeM3\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_email_format",
                table: "Users",
                sql: "\"Email\" ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_phone_e164",
                table: "Users",
                sql: "\"PhoneE164\" IS NULL OR \"PhoneE164\" ~ '^\\+[1-9][0-9]{1,14}$'");

            migrationBuilder.CreateIndex(
                name: "ux_trip_driver_live",
                table: "Trips",
                column: "DriverId",
                unique: true,
                filter: "\"Status\" IN ('Assigned','PickedUp','InTransit')");

            migrationBuilder.CreateIndex(
                name: "ux_trip_vehicle_live",
                table: "Trips",
                column: "VehicleId",
                unique: true,
                filter: "\"Status\" IN ('Assigned','PickedUp','InTransit')");

            migrationBuilder.CreateIndex(
                name: "uq_tripevidence_storagekey",
                table: "TripEvidences",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_tripevidence_type",
                table: "TripEvidences",
                columns: new[] { "TripId", "EvidenceType" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tripevent_lat",
                table: "TripEvents",
                sql: "\"SnapshotLat\" IS NULL OR \"SnapshotLat\" BETWEEN -90 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tripevent_lng",
                table: "TripEvents",
                sql: "\"SnapshotLng\" IS NULL OR \"SnapshotLng\" BETWEEN -180 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tripevent_transition",
                table: "TripEvents",
                sql: "\"FromStatus\" IS DISTINCT FROM \"ToStatus\"");

            migrationBuilder.CreateIndex(
                name: "uq_toolcall_attempt",
                table: "ToolCalls",
                columns: new[] { "AgentStepId", "ToolName", "AttemptNo" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_toolcall_allowlist",
                table: "ToolCalls",
                sql: "\"ToolName\" IN ('get_route_and_eta')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_toolcall_attempt",
                table: "ToolCalls",
                sql: "\"AttemptNo\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_toolcall_failure",
                table: "ToolCalls",
                sql: "\"Success\" = true OR \"ErrorMessage\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_shipperprofile_regno",
                table: "ShipperProfiles",
                column: "BusinessRegNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refreshtoken_user_active",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAt" },
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_refreshtoken_hash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_refreshtoken_expiry",
                table: "RefreshTokens",
                sql: "\"ExpiresAt\" > \"IssuedAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_refreshtoken_revoked",
                table: "RefreshTokens",
                sql: "\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"IssuedAt\"");

            migrationBuilder.CreateIndex(
                name: "uq_pwe_payloadhash",
                table: "PaymentWebhookEvents",
                column: "RawPayloadHash",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pwe_error",
                table: "PaymentWebhookEvents",
                sql: "\"ProcessingStatus\" <> 'Error' OR \"ErrorMessage\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_payment_attempt",
                table: "Payments",
                columns: new[] { "InvoiceId", "AttemptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_payment_gatewayref",
                table: "Payments",
                column: "GatewayRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_payment_success",
                table: "Payments",
                column: "InvoiceId",
                unique: true,
                filter: "\"Status\" = 'Success'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payment_amount",
                table: "Payments",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_payment_attempt",
                table: "Payments",
                sql: "\"AttemptNo\" >= 1");

            migrationBuilder.CreateIndex(
                name: "ix_notification_unread",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "CreatedAt" },
                descending: new[] { false, true },
                filter: "\"ReadAt\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_email_delivery",
                table: "Notifications",
                sql: "\"Channel\" <> 'Email' OR \"DeliveryStatus\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_failure",
                table: "Notifications",
                sql: "\"DeliveryStatus\" <> 'Failed' OR \"ErrorMessage\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_mc_run_agency",
                table: "MatchCandidates",
                columns: new[] { "WorkflowRunId", "AgencyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_mc_run_rank",
                table: "MatchCandidates",
                columns: new[] { "WorkflowRunId", "Rank" },
                unique: true,
                filter: "\"Eligible\" = true");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mc_reason",
                table: "MatchCandidates",
                sql: "\"Eligible\" = true OR \"RejectionReason\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mc_score",
                table: "MatchCandidates",
                sql: "\"EligibilityScore\" BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lsh_cancel_reason",
                table: "LoadStatusHistories",
                sql: "\"ToStatus\" <> 'Cancelled' OR \"Reason\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lsh_transition",
                table: "LoadStatusHistories",
                sql: "\"FromStatus\" IS DISTINCT FROM \"ToStatus\"");

            migrationBuilder.CreateIndex(
                name: "ix_load_posted",
                table: "Loads",
                column: "CreatedAt",
                filter: "\"Status\" = 'Posted'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_load_distinct_points",
                table: "Loads",
                sql: "\"PickupLat\" <> \"DropoffLat\" OR \"PickupLng\" <> \"DropoffLng\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_load_dropoff_lat",
                table: "Loads",
                sql: "\"DropoffLat\" BETWEEN -90 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "ck_load_dropoff_lng",
                table: "Loads",
                sql: "\"DropoffLng\" BETWEEN -180 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "ck_load_pickup_lat",
                table: "Loads",
                sql: "\"PickupLat\" BETWEEN -90 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "ck_load_pickup_lng",
                table: "Loads",
                sql: "\"PickupLng\" BETWEEN -180 AND 180");

            migrationBuilder.AddCheckConstraint(
                name: "ck_load_price",
                table: "Loads",
                sql: "\"EstimatedPrice\" IS NULL OR \"EstimatedPrice\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_load_volume",
                table: "Loads",
                sql: "\"VolumeM3\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_load_weight",
                table: "Loads",
                sql: "\"WeightKg\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_load_window",
                table: "Loads",
                sql: "\"PickupWindowEnd\" > \"PickupWindowStart\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_amount",
                table: "Invoices",
                sql: "\"Amount\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_currency",
                table: "Invoices",
                sql: "\"Currency\" ~ '^[A-Z]{3}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_due",
                table: "Invoices",
                sql: "\"DueDate\" IS NULL OR \"DueDate\" >= (\"IssuedAt\" AT TIME ZONE 'UTC')::date");

            migrationBuilder.CreateIndex(
                name: "uq_file_storagekey",
                table: "Files",
                column: "StorageKey",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_file_size",
                table: "Files",
                sql: "\"SizeBytes\" > 0 AND \"SizeBytes\" <= 10485760");

            migrationBuilder.CreateIndex(
                name: "ux_dispute_open",
                table: "Disputes",
                columns: new[] { "TripId", "Category" },
                unique: true,
                filter: "\"Status\" IN ('Open','UnderReview')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispute_description",
                table: "Disputes",
                sql: "length(trim(\"Description\")) >= 10");

            migrationBuilder.CreateIndex(
                name: "ux_compliancedoc_live",
                table: "ComplianceDocs",
                columns: new[] { "AgencyId", "DocType" },
                unique: true,
                filter: "\"Status\" IN ('Pending','Verified')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_compliancedoc_dates",
                table: "ComplianceDocs",
                sql: "\"ExpiresOn\" IS NULL OR \"ExpiresOn\" > \"IssuedOn\"");

            migrationBuilder.CreateIndex(
                name: "ux_assignment_live_per_load",
                table: "Assignments",
                column: "LoadId",
                unique: true,
                filter: "\"Status\" IN ('Proposed','Accepted')");

            migrationBuilder.CreateIndex(
                name: "ux_assignment_load_agency",
                table: "Assignments",
                columns: new[] { "LoadId", "AgencyId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_assignment_distance",
                table: "Assignments",
                sql: "\"RoutedDistanceKm\" IS NULL OR \"RoutedDistanceKm\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assignment_eta",
                table: "Assignments",
                sql: "\"ProposedEtaMinutes\" IS NULL OR \"ProposedEtaMinutes\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assignment_price",
                table: "Assignments",
                sql: "\"ProposedPrice\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ar_decline_reason",
                table: "AssignmentResponses",
                sql: "\"Response\" <> 'Declined' OR \"DeclineReason\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_ad_sequence",
                table: "ApprovalDecisions",
                columns: new[] { "WorkflowRunId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ad_single_approve",
                table: "ApprovalDecisions",
                column: "WorkflowRunId",
                unique: true,
                filter: "\"Decision\" = 'Approve'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ad_reason",
                table: "ApprovalDecisions",
                sql: "\"Decision\" = 'Approve' OR \"Reason\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ad_sequence",
                table: "ApprovalDecisions",
                sql: "\"SequenceNo\" >= 1");

            migrationBuilder.CreateIndex(
                name: "ix_awr_awaiting",
                table: "AgentWorkflowRuns",
                column: "StartedAt",
                filter: "\"Status\" = 'AwaitingApproval'");

            migrationBuilder.CreateIndex(
                name: "uq_awr_load_attempt",
                table: "AgentWorkflowRuns",
                columns: new[] { "LoadId", "AttemptNo" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_awr_attempt",
                table: "AgentWorkflowRuns",
                sql: "\"AttemptNo\" BETWEEN 1 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_awr_completed",
                table: "AgentWorkflowRuns",
                sql: "\"CompletedAt\" IS NULL OR \"CompletedAt\" >= \"StartedAt\"");

            migrationBuilder.CreateIndex(
                name: "uq_agentstep_order",
                table: "AgentSteps",
                columns: new[] { "WorkflowRunId", "StepNo" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_agentstep_failure",
                table: "AgentSteps",
                sql: "\"Status\" <> 'Failed' OR \"ErrorMessage\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agentstep_stepno",
                table: "AgentSteps",
                sql: "\"StepNo\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ash_transition",
                table: "AgencyStatusHistories",
                sql: "\"FromStatus\" IS DISTINCT FROM \"ToStatus\"");

            migrationBuilder.CreateIndex(
                name: "ix_agency_active_yard",
                table: "Agencies",
                columns: new[] { "YardLat", "YardLng" },
                filter: "\"Status\" = 'Active'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agency_yard_lat",
                table: "Agencies",
                sql: "\"YardLat\" BETWEEN -90 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agency_yard_lng",
                table: "Agencies",
                sql: "\"YardLng\" BETWEEN -180 AND 180");

            migrationBuilder.AddForeignKey(
                name: "FK_AgencyStaff_Users_UserId",
                table: "AgencyStaff",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AgencyStatusHistories_Agencies_AgencyId",
                table: "AgencyStatusHistories",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "AgencyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_AgentWorkflowRuns_WorkflowRunId",
                table: "ApprovalDecisions",
                column: "WorkflowRunId",
                principalTable: "AgentWorkflowRuns",
                principalColumn: "WorkflowRunId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentResponses_Assignments_AssignmentId",
                table: "AssignmentResponses",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ComplianceDocs_Agencies_AgencyId",
                table: "ComplianceDocs",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "AgencyId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DisputeResolutions_Disputes_DisputeId",
                table: "DisputeResolutions",
                column: "DisputeId",
                principalTable: "Disputes",
                principalColumn: "DisputeId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Loads_LoadId",
                table: "Files",
                column: "LoadId",
                principalTable: "Loads",
                principalColumn: "LoadId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoadStatusHistories_Loads_LoadId",
                table: "LoadStatusHistories",
                column: "LoadId",
                principalTable: "Loads",
                principalColumn: "LoadId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipperProfiles_Users_UserId",
                table: "ShipperProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TripEvents_Trips_TripId",
                table: "TripEvents",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TripEvidences_Trips_TripId",
                table: "TripEvidences",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Restrict);

            // trg_require_trip_evidence: cross-table gate with no Fluent API/CHECK-constraint
            // equivalent — blocks a Trip transitioning to PickedUp/Delivered without a matching
            // TripEvidence row of the corresponding EvidenceType.
            migrationBuilder.Sql("""
                CREATE FUNCTION fn_require_trip_evidence() RETURNS trigger AS $$
                BEGIN
                    IF NEW."Status" = 'PickedUp' AND OLD."Status" IS DISTINCT FROM 'PickedUp' THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM "TripEvidences"
                            WHERE "TripId" = NEW."TripId" AND "EvidenceType" = 'PickupProof'
                        ) THEN
                            RAISE EXCEPTION 'Trip % cannot transition to PickedUp without pickup evidence', NEW."TripId";
                        END IF;
                    END IF;

                    IF NEW."Status" = 'Delivered' AND OLD."Status" IS DISTINCT FROM 'Delivered' THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM "TripEvidences"
                            WHERE "TripId" = NEW."TripId" AND "EvidenceType" = 'DeliveryProof'
                        ) THEN
                            RAISE EXCEPTION 'Trip % cannot transition to Delivered without delivery evidence', NEW."TripId";
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_require_trip_evidence
                BEFORE UPDATE ON "Trips"
                FOR EACH ROW
                EXECUTE FUNCTION fn_require_trip_evidence();
                """);

            // fn_deny_mutation: generic append-only guard applied to every event-log-style table
            // (no UPDATE/DELETE after insert). Cross-cutting behavior with no Fluent API surface.
            migrationBuilder.Sql("""
                CREATE FUNCTION fn_deny_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION '% rows are append-only and cannot be updated or deleted', TG_TABLE_NAME;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_deny_mutation_loadstatushistories
                BEFORE UPDATE OR DELETE ON "LoadStatusHistories"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();

                CREATE TRIGGER trg_deny_mutation_agencystatushistories
                BEFORE UPDATE OR DELETE ON "AgencyStatusHistories"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();

                CREATE TRIGGER trg_deny_mutation_tripevents
                BEFORE UPDATE OR DELETE ON "TripEvents"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();

                CREATE TRIGGER trg_deny_mutation_tripevidences
                BEFORE UPDATE OR DELETE ON "TripEvidences"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();

                CREATE TRIGGER trg_deny_mutation_toolcalls
                BEFORE UPDATE OR DELETE ON "ToolCalls"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();

                CREATE TRIGGER trg_deny_mutation_approvaldecisions
                BEFORE UPDATE OR DELETE ON "ApprovalDecisions"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();

                CREATE TRIGGER trg_deny_mutation_paymentwebhookevents
                BEFORE UPDATE OR DELETE ON "PaymentWebhookEvents"
                FOR EACH ROW EXECUTE FUNCTION fn_deny_mutation();
                """);

            // fn_set_updated_at: maintains UpdatedAt on every mutable-entity table that has one.
            migrationBuilder.Sql("""
                CREATE FUNCTION fn_set_updated_at() RETURNS trigger AS $$
                BEGIN
                    NEW."UpdatedAt" = now();
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_set_updated_at_agencies
                BEFORE UPDATE ON "Agencies"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_agencystaff
                BEFORE UPDATE ON "AgencyStaff"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_agentworkflowruns
                BEFORE UPDATE ON "AgentWorkflowRuns"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_assignments
                BEFORE UPDATE ON "Assignments"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_compliancedocs
                BEFORE UPDATE ON "ComplianceDocs"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_disputes
                BEFORE UPDATE ON "Disputes"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_drivers
                BEFORE UPDATE ON "Drivers"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_invoices
                BEFORE UPDATE ON "Invoices"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_loads
                BEFORE UPDATE ON "Loads"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_shipperprofiles
                BEFORE UPDATE ON "ShipperProfiles"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_trips
                BEFORE UPDATE ON "Trips"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_users
                BEFORE UPDATE ON "Users"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();

                CREATE TRIGGER trg_set_updated_at_vehicles
                BEFORE UPDATE ON "Vehicles"
                FOR EACH ROW EXECUTE FUNCTION fn_set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse order: drop triggers before their functions, and before EF's own
            // DropForeignKey/DropCheckConstraint/DropIndex calls run below.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_set_updated_at_vehicles ON "Vehicles";
                DROP TRIGGER IF EXISTS trg_set_updated_at_users ON "Users";
                DROP TRIGGER IF EXISTS trg_set_updated_at_trips ON "Trips";
                DROP TRIGGER IF EXISTS trg_set_updated_at_shipperprofiles ON "ShipperProfiles";
                DROP TRIGGER IF EXISTS trg_set_updated_at_loads ON "Loads";
                DROP TRIGGER IF EXISTS trg_set_updated_at_invoices ON "Invoices";
                DROP TRIGGER IF EXISTS trg_set_updated_at_drivers ON "Drivers";
                DROP TRIGGER IF EXISTS trg_set_updated_at_disputes ON "Disputes";
                DROP TRIGGER IF EXISTS trg_set_updated_at_compliancedocs ON "ComplianceDocs";
                DROP TRIGGER IF EXISTS trg_set_updated_at_assignments ON "Assignments";
                DROP TRIGGER IF EXISTS trg_set_updated_at_agentworkflowruns ON "AgentWorkflowRuns";
                DROP TRIGGER IF EXISTS trg_set_updated_at_agencystaff ON "AgencyStaff";
                DROP TRIGGER IF EXISTS trg_set_updated_at_agencies ON "Agencies";
                DROP FUNCTION IF EXISTS fn_set_updated_at();

                DROP TRIGGER IF EXISTS trg_deny_mutation_paymentwebhookevents ON "PaymentWebhookEvents";
                DROP TRIGGER IF EXISTS trg_deny_mutation_approvaldecisions ON "ApprovalDecisions";
                DROP TRIGGER IF EXISTS trg_deny_mutation_toolcalls ON "ToolCalls";
                DROP TRIGGER IF EXISTS trg_deny_mutation_tripevidences ON "TripEvidences";
                DROP TRIGGER IF EXISTS trg_deny_mutation_tripevents ON "TripEvents";
                DROP TRIGGER IF EXISTS trg_deny_mutation_agencystatushistories ON "AgencyStatusHistories";
                DROP TRIGGER IF EXISTS trg_deny_mutation_loadstatushistories ON "LoadStatusHistories";
                DROP FUNCTION IF EXISTS fn_deny_mutation();

                DROP TRIGGER IF EXISTS trg_require_trip_evidence ON "Trips";
                DROP FUNCTION IF EXISTS fn_require_trip_evidence();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_AgencyStaff_Users_UserId",
                table: "AgencyStaff");

            migrationBuilder.DropForeignKey(
                name: "FK_AgencyStatusHistories_Agencies_AgencyId",
                table: "AgencyStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalDecisions_AgentWorkflowRuns_WorkflowRunId",
                table: "ApprovalDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentResponses_Assignments_AssignmentId",
                table: "AssignmentResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_ComplianceDocs_Agencies_AgencyId",
                table: "ComplianceDocs");

            migrationBuilder.DropForeignKey(
                name: "FK_DisputeResolutions_Disputes_DisputeId",
                table: "DisputeResolutions");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Loads_LoadId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_LoadStatusHistories_Loads_LoadId",
                table: "LoadStatusHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ShipperProfiles_Users_UserId",
                table: "ShipperProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_TripEvents_Trips_TripId",
                table: "TripEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_TripEvidences_Trips_TripId",
                table: "TripEvidences");

            migrationBuilder.DropIndex(
                name: "uq_vehicle_agency_regno",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_capacity",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicle_volume",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_email_format",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_phone_e164",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "ux_trip_driver_live",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "ux_trip_vehicle_live",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "uq_tripevidence_storagekey",
                table: "TripEvidences");

            migrationBuilder.DropIndex(
                name: "uq_tripevidence_type",
                table: "TripEvidences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tripevent_lat",
                table: "TripEvents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tripevent_lng",
                table: "TripEvents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tripevent_transition",
                table: "TripEvents");

            migrationBuilder.DropIndex(
                name: "uq_toolcall_attempt",
                table: "ToolCalls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_toolcall_allowlist",
                table: "ToolCalls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_toolcall_attempt",
                table: "ToolCalls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_toolcall_failure",
                table: "ToolCalls");

            migrationBuilder.DropIndex(
                name: "uq_shipperprofile_regno",
                table: "ShipperProfiles");

            migrationBuilder.DropIndex(
                name: "ix_refreshtoken_user_active",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "uq_refreshtoken_hash",
                table: "RefreshTokens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_refreshtoken_expiry",
                table: "RefreshTokens");

            migrationBuilder.DropCheckConstraint(
                name: "ck_refreshtoken_revoked",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "uq_pwe_payloadhash",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pwe_error",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "uq_payment_attempt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "uq_payment_gatewayref",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "ux_payment_success",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payment_amount",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_payment_attempt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "ix_notification_unread",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_email_delivery",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_failure",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "uq_mc_run_agency",
                table: "MatchCandidates");

            migrationBuilder.DropIndex(
                name: "ux_mc_run_rank",
                table: "MatchCandidates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mc_reason",
                table: "MatchCandidates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mc_score",
                table: "MatchCandidates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lsh_cancel_reason",
                table: "LoadStatusHistories");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lsh_transition",
                table: "LoadStatusHistories");

            migrationBuilder.DropIndex(
                name: "ix_load_posted",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_load_distinct_points",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_load_dropoff_lat",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_load_dropoff_lng",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_load_pickup_lat",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_load_pickup_lng",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_load_price",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_load_volume",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_load_weight",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_load_window",
                table: "Loads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_amount",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_currency",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_due",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "uq_file_storagekey",
                table: "Files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_file_size",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "ux_dispute_open",
                table: "Disputes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispute_description",
                table: "Disputes");

            migrationBuilder.DropIndex(
                name: "ux_compliancedoc_live",
                table: "ComplianceDocs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_compliancedoc_dates",
                table: "ComplianceDocs");

            migrationBuilder.DropIndex(
                name: "ux_assignment_live_per_load",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "ux_assignment_load_agency",
                table: "Assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assignment_distance",
                table: "Assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assignment_eta",
                table: "Assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assignment_price",
                table: "Assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ar_decline_reason",
                table: "AssignmentResponses");

            migrationBuilder.DropIndex(
                name: "uq_ad_sequence",
                table: "ApprovalDecisions");

            migrationBuilder.DropIndex(
                name: "ux_ad_single_approve",
                table: "ApprovalDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ad_reason",
                table: "ApprovalDecisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ad_sequence",
                table: "ApprovalDecisions");

            migrationBuilder.DropIndex(
                name: "ix_awr_awaiting",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "uq_awr_load_attempt",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropCheckConstraint(
                name: "ck_awr_attempt",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropCheckConstraint(
                name: "ck_awr_completed",
                table: "AgentWorkflowRuns");

            migrationBuilder.DropIndex(
                name: "uq_agentstep_order",
                table: "AgentSteps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agentstep_failure",
                table: "AgentSteps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agentstep_stepno",
                table: "AgentSteps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ash_transition",
                table: "AgencyStatusHistories");

            migrationBuilder.DropIndex(
                name: "ix_agency_active_yard",
                table: "Agencies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agency_yard_lat",
                table: "Agencies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agency_yard_lng",
                table: "Agencies");

            migrationBuilder.RenameIndex(
                name: "uq_user_email",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.RenameIndex(
                name: "uq_load_reference",
                table: "Loads",
                newName: "IX_Loads_ReferenceCode");

            migrationBuilder.RenameIndex(
                name: "uq_invoice_number",
                table: "Invoices",
                newName: "IX_Invoices_InvoiceNumber");

            migrationBuilder.RenameIndex(
                name: "uq_agency_regno",
                table: "Agencies",
                newName: "IX_Agencies_BusinessRegNo");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_AgencyId",
                table: "Vehicles",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RegistrationNo",
                table: "Vehicles",
                column: "RegistrationNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverId",
                table: "Trips",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_VehicleId",
                table: "Trips",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvidences_TripId",
                table: "TripEvidences",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCalls_AgentStepId",
                table: "ToolCalls",
                column: "AgentStepId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_InvoiceId",
                table: "Payments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchCandidates_WorkflowRunId",
                table: "MatchCandidates",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_TripId",
                table: "Disputes",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceDocs_AgencyId",
                table: "ComplianceDocs",
                column: "AgencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_LoadId",
                table: "Assignments",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_WorkflowRunId",
                table: "ApprovalDecisions",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentWorkflowRuns_LoadId",
                table: "AgentWorkflowRuns",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSteps_WorkflowRunId",
                table: "AgentSteps",
                column: "WorkflowRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgencyStaff_Users_UserId",
                table: "AgencyStaff",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AgencyStatusHistories_Agencies_AgencyId",
                table: "AgencyStatusHistories",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "AgencyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalDecisions_AgentWorkflowRuns_WorkflowRunId",
                table: "ApprovalDecisions",
                column: "WorkflowRunId",
                principalTable: "AgentWorkflowRuns",
                principalColumn: "WorkflowRunId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentResponses_Assignments_AssignmentId",
                table: "AssignmentResponses",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "AssignmentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ComplianceDocs_Agencies_AgencyId",
                table: "ComplianceDocs",
                column: "AgencyId",
                principalTable: "Agencies",
                principalColumn: "AgencyId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DisputeResolutions_Disputes_DisputeId",
                table: "DisputeResolutions",
                column: "DisputeId",
                principalTable: "Disputes",
                principalColumn: "DisputeId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Loads_LoadId",
                table: "Files",
                column: "LoadId",
                principalTable: "Loads",
                principalColumn: "LoadId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LoadStatusHistories_Loads_LoadId",
                table: "LoadStatusHistories",
                column: "LoadId",
                principalTable: "Loads",
                principalColumn: "LoadId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShipperProfiles_Users_UserId",
                table: "ShipperProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TripEvents_Trips_TripId",
                table: "TripEvents",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TripEvidences_Trips_TripId",
                table: "TripEvidences",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "TripId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
