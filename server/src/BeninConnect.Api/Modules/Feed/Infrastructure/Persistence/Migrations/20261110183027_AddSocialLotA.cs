using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Feed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialLotA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Images",
                schema: "feed",
                table: "posts",
                type: "text[]",
                nullable: false,
                defaultValueSql: "\'{}\'");

            migrationBuilder.AddColumn<List<Guid>>(
                name: "MentionIds",
                schema: "feed",
                table: "posts",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "\'{}\'");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PinnedAt",
                schema: "feed",
                table: "posts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedCommentId",
                schema: "feed",
                table: "posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Tags",
                schema: "feed",
                table: "posts",
                type: "text[]",
                nullable: false,
                defaultValueSql: "\'{}\'");

            migrationBuilder.AddColumn<List<Guid>>(
                name: "MentionIds",
                schema: "feed",
                table: "comments",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "\'{}\'");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                schema: "feed",
                table: "comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "saved_posts",
                schema: "feed",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_posts", x => new { x.UserId, x.PostId });
                    table.ForeignKey(
                        name: "FK_saved_posts_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "feed",
                        principalTable: "posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_posts_Tags",
                schema: "feed",
                table: "posts",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_comments_ParentId",
                schema: "feed",
                table: "comments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_saved_posts_PostId",
                schema: "feed",
                table: "saved_posts",
                column: "PostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_posts",
                schema: "feed");

            migrationBuilder.DropIndex(
                name: "IX_posts_Tags",
                schema: "feed",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_comments_ParentId",
                schema: "feed",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "Images",
                schema: "feed",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "MentionIds",
                schema: "feed",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                schema: "feed",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "PinnedCommentId",
                schema: "feed",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "Tags",
                schema: "feed",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "MentionIds",
                schema: "feed",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "ParentId",
                schema: "feed",
                table: "comments");
        }
    }
}
