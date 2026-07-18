using System.Text.Json;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.FinderHub;

/// <summary>
/// Matches a user's answers (JSON) against a collection of FinderResult conditions.
///
/// AnswersJson format: { "1": "car", "2": "new", "3": "colorado" }
///   Key = StepOrder (as string), Value = selected option Value.
///
/// ConditionJson format (per result):
///   { "1": "car", "2": "new" }                   — all keys must match exactly
///   { "1": ["car", "truck"], "2": "new" }         — step 1 can be car OR truck
///   {}                                             — default/catch-all (always matches)
///
/// Matching algorithm:
///   1. Parse both JSON objects.
///   2. For each condition key, check the answer matches (string or array OR).
///   3. The first result whose ALL condition keys are satisfied wins.
///   4. A result with {} condition is a catch-all — put it last.
///
/// Returns the ProductKey of the winning result, or null if no match.
/// </summary>
public static class FinderResultMatcher
{
    public static string? Match(string answersJson, IEnumerable<FinderResult> results)
    {
        Dictionary<string, JsonElement> answers;
        try
        {
            answers = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(answersJson)
                      ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            return null;
        }

        // Sort: catch-alls (empty conditions) last so specific rules win first
        var sorted = results
            .OrderBy(r =>
            {
                try
                {
                    var cond = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(r.ConditionJson);
                    return cond is null || cond.Count == 0 ? 1 : 0;
                }
                catch { return 1; }
            })
            .ToList();

        foreach (var result in sorted)
        {
            if (Matches(answers, result.ConditionJson))
                return result.ProductKey;
        }

        return null;
    }

    private static bool Matches(
        Dictionary<string, JsonElement> answers,
        string conditionJson)
    {
        Dictionary<string, JsonElement> conditions;
        try
        {
            conditions = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(conditionJson)
                         ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            return false;
        }

        // Empty condition = catch-all
        if (conditions.Count == 0)
            return true;

        foreach (var (stepKey, condValue) in conditions)
        {
            if (!answers.TryGetValue(stepKey, out var answerElement))
                return false; // user didn't answer this step

            var answerStr = answerElement.ValueKind == JsonValueKind.String
                ? answerElement.GetString() ?? string.Empty
                : answerElement.ToString();

            if (condValue.ValueKind == JsonValueKind.Array)
            {
                // OR matching — any value in array is acceptable
                bool any = false;
                foreach (var item in condValue.EnumerateArray())
                {
                    var itemStr = item.ValueKind == JsonValueKind.String
                        ? item.GetString() ?? string.Empty
                        : item.ToString();

                    if (string.Equals(answerStr, itemStr, StringComparison.OrdinalIgnoreCase))
                    {
                        any = true;
                        break;
                    }
                }
                if (!any) return false;
            }
            else
            {
                // Exact match
                var condStr = condValue.ValueKind == JsonValueKind.String
                    ? condValue.GetString() ?? string.Empty
                    : condValue.ToString();

                if (!string.Equals(answerStr, condStr, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }
}
