using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeroPassport.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HeroPassportDbContext))]
[Migration("0001_Initial")]
public sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE heroes (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL CHECK(length(name) BETWEEN 1 AND 64),
                total_xp INTEGER NOT NULL DEFAULT 0 CHECK(total_xp BETWEEN 0 AND 9007199254740991),
                trust INTEGER NOT NULL DEFAULT 50 CHECK(trust BETWEEN 0 AND 100),
                strain INTEGER NOT NULL DEFAULT 20 CHECK(strain BETWEEN 0 AND 100),
                success_streak INTEGER NOT NULL DEFAULT 0 CHECK(success_streak >= 0),
                archived_at_utc TEXT NULL,
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE projects (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL CHECK(length(display_name) BETWEEN 1 AND 120),
                workspace_fingerprint TEXT NOT NULL UNIQUE CHECK(length(workspace_fingerprint) = 64),
                identity_version TEXT NOT NULL CHECK(identity_version = 'project-identity/1'),
                created_at_utc TEXT NOT NULL
            );

            CREATE TABLE app_settings (
                id INTEGER PRIMARY KEY CHECK(id = 1),
                setup_completed INTEGER NOT NULL CHECK(setup_completed IN (0,1)),
                active_hero_id TEXT NULL REFERENCES heroes(id) ON DELETE RESTRICT,
                locale TEXT NOT NULL CHECK(locale IN ('ru-RU','en-US')),
                presentation_style TEXT NOT NULL CHECK(presentation_style IN ('rpg_engineering','classic_rpg','minimal')),
                auto_start_quest INTEGER NOT NULL CHECK(auto_start_quest IN (0,1)),
                auto_finish_quest INTEGER NOT NULL CHECK(auto_finish_quest IN (0,1)),
                project_identity_salt_v1 BLOB NOT NULL CHECK(length(project_identity_salt_v1) = 32),
                config_version INTEGER NOT NULL CHECK(config_version >= 1),
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL,
                CHECK(
                    (setup_completed = 0 AND active_hero_id IS NULL)
                    OR
                    (setup_completed = 1 AND active_hero_id IS NOT NULL)
                )
            );

            CREATE TABLE hero_project_stats (
                hero_id TEXT NOT NULL REFERENCES heroes(id) ON DELETE CASCADE,
                project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE RESTRICT,
                quests_started INTEGER NOT NULL DEFAULT 0 CHECK(quests_started >= 0),
                quests_finished INTEGER NOT NULL DEFAULT 0 CHECK(quests_finished >= 0),
                quests_succeeded INTEGER NOT NULL DEFAULT 0 CHECK(quests_succeeded >= 0),
                total_xp_earned INTEGER NOT NULL DEFAULT 0 CHECK(total_xp_earned BETWEEN 0 AND 9007199254740991),
                last_quest_at_utc TEXT NULL,
                PRIMARY KEY(hero_id, project_id)
            );

            CREATE TABLE mutation_receipts (
                operation_key TEXT NOT NULL CHECK(operation_key IN ('bootstrap','create_hero','start_quest','finish_quest')),
                request_id TEXT NOT NULL,
                args_encoding_version TEXT NOT NULL,
                args_hash BLOB NOT NULL CHECK(length(args_hash) = 32),
                result_kind TEXT NOT NULL CHECK(result_kind IN ('bootstrap','hero','quest_start','quest_finish')),
                result_entity_id TEXT NULL,
                project_id TEXT NULL,
                hero_id TEXT NULL,
                result_status TEXT NOT NULL CHECK(result_status IN ('active','target_deleted')),
                effective_at_utc TEXT NOT NULL,
                PRIMARY KEY(operation_key, request_id)
            );

            CREATE TABLE quest_sessions (
                id TEXT PRIMARY KEY,
                hero_id TEXT NOT NULL REFERENCES heroes(id) ON DELETE CASCADE,
                project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE RESTRICT,
                quest_type TEXT NOT NULL CHECK(quest_type IN ('planning','research','coding','review','debugging','documentation','maintenance')),
                title TEXT NOT NULL CHECK(length(title) BETWEEN 1 AND 120),
                goal TEXT NOT NULL CHECK(length(goal) BETWEEN 1 AND 500),
                locale TEXT NOT NULL CHECK(locale IN ('ru-RU','en-US')),
                status TEXT NOT NULL CHECK(status IN ('open','finished')),
                started_at_utc TEXT NOT NULL,
                finished_at_utc TEXT NULL,
                created_at_utc TEXT NOT NULL,
                CHECK(
                    (status = 'open' AND finished_at_utc IS NULL)
                    OR
                    (status = 'finished' AND finished_at_utc IS NOT NULL)
                )
            );

            CREATE UNIQUE INDEX ux_quest_sessions_one_open_per_hero_project
                ON quest_sessions(hero_id, project_id)
                WHERE status = 'open';

            CREATE TABLE quest_reports (
                id TEXT PRIMARY KEY,
                quest_id TEXT NOT NULL UNIQUE REFERENCES quest_sessions(id) ON DELETE CASCADE,
                result TEXT NOT NULL CHECK(result IN ('success','partial','blocked','failed','abandoned')),
                summary TEXT NOT NULL CHECK(length(summary) BETWEEN 1 AND 2000),
                tests_mentioned INTEGER NOT NULL CHECK(tests_mentioned IN (0,1)),
                scope_violations INTEGER NOT NULL CHECK(scope_violations BETWEEN 0 AND 20),
                user_corrections INTEGER NOT NULL CHECK(user_corrections BETWEEN 0 AND 20),
                build_status TEXT NOT NULL CHECK(build_status IN ('not_run','passed','failed','unknown')),
                build_evidence TEXT NOT NULL CHECK(build_evidence IN ('observed','reported','none')),
                tests_status TEXT NOT NULL CHECK(tests_status IN ('not_run','passed','failed','unknown')),
                tests_evidence TEXT NOT NULL CHECK(tests_evidence IN ('observed','reported','none')),
                finalization_args_encoding_version TEXT NOT NULL,
                finalization_args_hash BLOB NOT NULL CHECK(length(finalization_args_hash) = 32),
                reward_rule_version TEXT NOT NULL,
                hero_progression_version TEXT NOT NULL,
                skill_progression_version TEXT NOT NULL,
                skill_allocation_version TEXT NOT NULL,
                trust_strain_rule_version TEXT NOT NULL,
                streak_rule_version TEXT NOT NULL,
                unlock_rule_version TEXT NOT NULL,
                rank_rule_version TEXT NOT NULL,
                base_xp INTEGER NOT NULL CHECK(base_xp >= 0),
                bonus_xp INTEGER NOT NULL CHECK(bonus_xp >= 0),
                penalty_xp INTEGER NOT NULL CHECK(penalty_xp >= 0),
                raw_xp INTEGER NOT NULL CHECK(raw_xp >= 0),
                outcome_permille INTEGER NOT NULL CHECK(outcome_permille BETWEEN 0 AND 1000),
                xp_gained INTEGER NOT NULL CHECK(xp_gained BETWEEN 0 AND 9007199254740991),
                hero_total_xp_before INTEGER NOT NULL CHECK(hero_total_xp_before BETWEEN 0 AND 9007199254740991),
                hero_total_xp_after INTEGER NOT NULL CHECK(hero_total_xp_after BETWEEN 0 AND 9007199254740991),
                hero_level_before INTEGER NOT NULL CHECK(hero_level_before >= 1),
                hero_level_after INTEGER NOT NULL CHECK(hero_level_after >= 1),
                rank_before TEXT NOT NULL,
                rank_after TEXT NOT NULL,
                trust_before INTEGER NOT NULL CHECK(trust_before BETWEEN 0 AND 100),
                trust_after INTEGER NOT NULL CHECK(trust_after BETWEEN 0 AND 100),
                strain_before INTEGER NOT NULL CHECK(strain_before BETWEEN 0 AND 100),
                strain_after INTEGER NOT NULL CHECK(strain_after BETWEEN 0 AND 100),
                streak_before INTEGER NOT NULL CHECK(streak_before >= 0),
                streak_after INTEGER NOT NULL CHECK(streak_after >= 0),
                active_title_before TEXT NULL,
                active_title_after TEXT NULL,
                created_at_utc TEXT NOT NULL
            );

            CREATE TABLE quest_reward_components (
                quest_report_id TEXT NOT NULL REFERENCES quest_reports(id) ON DELETE CASCADE,
                ordinal INTEGER NOT NULL CHECK(ordinal >= 0),
                component_key TEXT NOT NULL,
                xp_delta INTEGER NOT NULL,
                PRIMARY KEY(quest_report_id, ordinal)
            );

            CREATE TABLE quest_trust_strain_components (
                quest_report_id TEXT NOT NULL REFERENCES quest_reports(id) ON DELETE CASCADE,
                ordinal INTEGER NOT NULL CHECK(ordinal >= 0),
                component_key TEXT NOT NULL,
                trust_delta INTEGER NOT NULL,
                strain_delta INTEGER NOT NULL,
                PRIMARY KEY(quest_report_id, ordinal)
            );

            CREATE TABLE skills (
                skill_key TEXT PRIMARY KEY,
                catalog_version TEXT NOT NULL
            );

            CREATE TABLE hero_skills (
                hero_id TEXT NOT NULL REFERENCES heroes(id) ON DELETE CASCADE,
                skill_key TEXT NOT NULL REFERENCES skills(skill_key) ON DELETE RESTRICT,
                xp INTEGER NOT NULL CHECK(xp BETWEEN 0 AND 9007199254740991),
                updated_at_utc TEXT NOT NULL,
                PRIMARY KEY(hero_id, skill_key)
            );

            CREATE TABLE quest_report_skills (
                quest_report_id TEXT NOT NULL REFERENCES quest_reports(id) ON DELETE CASCADE,
                ordinal INTEGER NOT NULL CHECK(ordinal BETWEEN 0 AND 2),
                skill_key TEXT NOT NULL REFERENCES skills(skill_key) ON DELETE RESTRICT,
                xp_gained INTEGER NOT NULL CHECK(xp_gained >= 0),
                xp_before INTEGER NOT NULL CHECK(xp_before >= 0),
                xp_after INTEGER NOT NULL CHECK(xp_after >= 0),
                level_before INTEGER NOT NULL CHECK(level_before >= 1),
                level_after INTEGER NOT NULL CHECK(level_after >= 1),
                PRIMARY KEY(quest_report_id, ordinal),
                UNIQUE(quest_report_id, skill_key)
            );

            CREATE TABLE traits (
                trait_key TEXT PRIMARY KEY,
                catalog_version TEXT NOT NULL
            );

            CREATE TABLE hero_traits (
                hero_id TEXT NOT NULL REFERENCES heroes(id) ON DELETE CASCADE,
                trait_key TEXT NOT NULL REFERENCES traits(trait_key) ON DELETE RESTRICT,
                unlocked_at_utc TEXT NOT NULL,
                source_quest_id TEXT NULL REFERENCES quest_sessions(id) ON DELETE SET NULL,
                PRIMARY KEY(hero_id, trait_key)
            );

            CREATE TABLE titles (
                title_key TEXT PRIMARY KEY,
                priority INTEGER NOT NULL,
                catalog_version TEXT NOT NULL
            );

            CREATE TABLE hero_titles (
                hero_id TEXT NOT NULL REFERENCES heroes(id) ON DELETE CASCADE,
                title_key TEXT NOT NULL REFERENCES titles(title_key) ON DELETE RESTRICT,
                unlocked_at_utc TEXT NOT NULL,
                source_quest_id TEXT NULL REFERENCES quest_sessions(id) ON DELETE SET NULL,
                PRIMARY KEY(hero_id, title_key)
            );

            CREATE TABLE quest_milestones (
                quest_report_id TEXT NOT NULL REFERENCES quest_reports(id) ON DELETE CASCADE,
                ordinal INTEGER NOT NULL CHECK(ordinal >= 0),
                event_key TEXT NOT NULL,
                semantic_key TEXT NOT NULL,
                PRIMARY KEY(quest_report_id, ordinal)
            );

            CREATE TABLE xp_events (
                id TEXT PRIMARY KEY,
                quest_id TEXT NOT NULL UNIQUE REFERENCES quest_sessions(id) ON DELETE CASCADE,
                hero_id TEXT NOT NULL REFERENCES heroes(id) ON DELETE CASCADE,
                project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE RESTRICT,
                xp_delta INTEGER NOT NULL CHECK(xp_delta >= 0),
                reward_rule_version TEXT NOT NULL,
                created_at_utc TEXT NOT NULL
            );

            INSERT INTO app_settings(
                id, setup_completed, active_hero_id, locale, presentation_style,
                auto_start_quest, auto_finish_quest, project_identity_salt_v1,
                config_version, created_at_utc, updated_at_utc)
            VALUES(
                1, 0, NULL, 'en-US', 'rpg_engineering',
                1, 1, randomblob(32),
                1, strftime('%Y-%m-%dT%H:%M:%fZ','now'), strftime('%Y-%m-%dT%H:%M:%fZ','now'));

            INSERT INTO skills(skill_key, catalog_version) VALUES
                ('coding','skills/1.0.0'),
                ('testing_awareness','skills/1.0.0'),
                ('scope_control','skills/1.0.0'),
                ('documentation','skills/1.0.0'),
                ('tool_use','skills/1.0.0'),
                ('planning','skills/1.0.0'),
                ('research','skills/1.0.0'),
                ('debugging','skills/1.0.0'),
                ('review','skills/1.0.0'),
                ('maintenance','skills/1.0.0');

            INSERT INTO traits(trait_key, catalog_version) VALUES
                ('precise_executor','unlock/2.0.0'),
                ('test_scout','unlock/2.0.0'),
                ('scope_keeper','unlock/2.0.0'),
                ('steady_hand','unlock/2.0.0'),
                ('polyglot_crafter','unlock/2.0.0');

            INSERT INTO titles(title_key, priority, catalog_version) VALUES
                ('rising_adventurer',1,'unlock/2.0.0'),
                ('veteran_of_the_merge',2,'unlock/2.0.0'),
                ('skill_specialist',3,'unlock/2.0.0'),
                ('unbroken_builder',4,'unlock/2.0.0'),
                ('master_of_many_tools',5,'unlock/2.0.0');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS xp_events;
            DROP TABLE IF EXISTS quest_milestones;
            DROP TABLE IF EXISTS hero_titles;
            DROP TABLE IF EXISTS titles;
            DROP TABLE IF EXISTS hero_traits;
            DROP TABLE IF EXISTS traits;
            DROP TABLE IF EXISTS quest_report_skills;
            DROP TABLE IF EXISTS hero_skills;
            DROP TABLE IF EXISTS skills;
            DROP TABLE IF EXISTS quest_trust_strain_components;
            DROP TABLE IF EXISTS quest_reward_components;
            DROP TABLE IF EXISTS quest_reports;
            DROP INDEX IF EXISTS ux_quest_sessions_one_open_per_hero_project;
            DROP TABLE IF EXISTS quest_sessions;
            DROP TABLE IF EXISTS mutation_receipts;
            DROP TABLE IF EXISTS hero_project_stats;
            DROP TABLE IF EXISTS app_settings;
            DROP TABLE IF EXISTS projects;
            DROP TABLE IF EXISTS heroes;
            """);
    }
}
