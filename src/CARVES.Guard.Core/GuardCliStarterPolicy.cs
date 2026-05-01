namespace Carves.Guard.Core;

public static partial class GuardCliRunner
{
    private static string CreateGuardStarterPolicyJson()
    {
        return """
            {
              "schema_version": 1,
              "policy_id": "carves-guard-starter",
              "path_policy": {
                "path_case": "case_sensitive",
                "allowed_path_prefixes": [ "src/", "tests/", "docs/", "README.md", "package.json", "package-lock.json", "*.csproj" ],
                "protected_path_prefixes": [ ".git/", ".ai/tasks/", ".ai/memory/" ],
                "outside_allowed_action": "review",
                "protected_path_action": "block"
              },
              "change_budget": {
                "max_changed_files": 8,
                "max_total_additions": 300,
                "max_total_deletions": 300,
                "max_file_additions": 150,
                "max_file_deletions": 150,
                "max_renames": 2
              },
              "dependency_policy": {
                "manifest_paths": [ "package.json", "*.csproj", "pyproject.toml", "requirements.txt" ],
                "lockfile_paths": [ "package-lock.json", "packages.lock.json", "poetry.lock", "uv.lock" ],
                "manifest_without_lockfile_action": "review",
                "lockfile_without_manifest_action": "review",
                "new_dependency_action": "review"
              },
              "change_shape": {
                "allow_rename_with_content_change": false,
                "allow_delete_without_replacement": false,
                "generated_path_prefixes": [ "dist/", "build/", "coverage/", "bin/", "obj/" ],
                "generated_path_action": "review",
                "mixed_feature_and_refactor_action": "review",
                "require_tests_for_source_changes": true,
                "source_path_prefixes": [ "src/" ],
                "test_path_prefixes": [ "tests/" ],
                "missing_tests_action": "review"
              },
              "decision": {
                "fail_closed": true,
                "default_outcome": "allow",
                "review_is_passing": false,
                "emit_evidence": true
              }
            }
            """;
    }
}
