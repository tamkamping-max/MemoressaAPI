using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoressa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFramePackageExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "frame_playback_packages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_frame_playback_packages_DisplayDeviceId_ExternalId",
                table: "frame_playback_packages",
                columns: new[] { "DisplayDeviceId", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_frame_playback_packages_DisplayDeviceId_ExternalId",
                table: "frame_playback_packages");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "frame_playback_packages");
        }
    }
}
