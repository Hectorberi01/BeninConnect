using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BeninConnect.Api.Modules.Feed.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "feed");

            migrationBuilder.CreateTable(
                name: "posts",
                schema: "feed",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ImagePath = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CommentCount = table.Column<int>(type: "integer", nullable: false),
                    ShareCount = table.Column<int>(type: "integer", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    HiddenReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "feed",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comments_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "feed",
                        principalTable: "posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_likes",
                schema: "feed",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LikedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_likes", x => new { x.PostId, x.UserId });
                    table.ForeignKey(
                        name: "FK_post_likes_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "feed",
                        principalTable: "posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comment_reactions",
                schema: "feed",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReactedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comment_reactions", x => new { x.CommentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_comment_reactions_comments_CommentId",
                        column: x => x.CommentId,
                        principalSchema: "feed",
                        principalTable: "comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comments_AuthorId",
                schema: "feed",
                table: "comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_comments_PostId",
                schema: "feed",
                table: "comments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_AuthorId",
                schema: "feed",
                table: "posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_CommunityId",
                schema: "feed",
                table: "posts",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_CreatedAt",
                schema: "feed",
                table: "posts",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comment_reactions",
                schema: "feed");

            migrationBuilder.DropTable(
                name: "post_likes",
                schema: "feed");

            migrationBuilder.DropTable(
                name: "comments",
                schema: "feed");

            migrationBuilder.DropTable(
                name: "posts",
                schema: "feed");
        }
    }
}
