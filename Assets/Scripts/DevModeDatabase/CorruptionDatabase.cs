using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads corruptions from a CSV TextAsset (Assets/Database/Corruptions.csv).
/// Column layout (0-indexed):
///   0=id, 1=Ready 4 Publish, 2=Requires Teammate, 3=points, 4=Pack, 5=Type,
///   6=Title(EN), 7=Description(EN), 8=Short Description(EN),
///   9=Title(IS), 10=Description(IS), 11=Short Description(IS)
/// Rows where Ready 4 Publish != TRUE are skipped.
/// </summary>
public class CorruptionDatabase : MonoBehaviour
{
    [Tooltip("Drag Assets/Database/Corruptions.csv here.")]
    [SerializeField] private TextAsset corruptionsCSV;

    public List<Corruption> LoadCorruptions()
    {
        var objectives = new List<Corruption>();

        if (corruptionsCSV == null)
        {
            Debug.LogError("CorruptionDatabase: corruptionsCSV is not assigned. No corruptions loaded.");
            return objectives;
        }

        var rows   = ParseCSV(corruptionsCSV.text);
        int nextId = 0;

        foreach (var fields in rows)
        {
            if (fields.Length < 6) continue;

            // Skip header rows
            string firstField = Get(fields, 0);
            if (firstField == "id" || firstField == "Properties") continue;

            // Skip rows not marked as ready to publish
            string readyStr = Get(fields, 1);
            if (!string.Equals(readyStr, "TRUE", System.StringComparison.OrdinalIgnoreCase)) continue;

            string title = Get(fields, 6);
            if (string.IsNullOrWhiteSpace(title)) continue;

            string typeStr = Get(fields, 5);
            if (!TryParseCorruptionType(typeStr, out GameManager.CorruptionType type)) continue;

            if (!int.TryParse(Get(fields, 3), out int points) || points <= 0)
                points = 60;

            if (!int.TryParse(firstField, out int id))
                id = nextId;
            nextId = id + 1;

            bool requiresTeammate = string.Equals(Get(fields, 2), "TRUE", System.StringComparison.OrdinalIgnoreCase);

            string descEn = Get(fields, 7);
            bool requiresZeroGroupVotes = type == GameManager.CorruptionType.Betrayal
                && descEn.IndexOf("zero votes", System.StringComparison.OrdinalIgnoreCase) >= 0;

            objectives.Add(new Corruption
            {
                id                    = id,
                title                 = title,
                titleIs               = Get(fields, 9),
                description           = descEn,
                descriptionIs         = Get(fields, 10),
                shortDescription      = Get(fields, 8),
                shortDescriptionIs    = Get(fields, 11),
                points                = points,
                type                  = type,
                requiresTeammate      = requiresTeammate,
                requiresZeroGroupVotes = requiresZeroGroupVotes,
                assignedPlayerId      = -1,
                neededCount           = null,
                achievedCount         = null,
                completeted           = false,
            });
        }

        Debug.Log($"CorruptionDatabase: Loaded {objectives.Count} corruptions from CSV.");
        return objectives;
    }

    // -------------------- Parsing Helpers --------------------

    private static bool TryParseCorruptionType(string s, out GameManager.CorruptionType type)
    {
        switch (s.Trim())
        {
            case "Speech":       type = GameManager.CorruptionType.Speech;       return true;
            case "Interruption": type = GameManager.CorruptionType.Interruption; return true;
            case "Betrayal":     type = GameManager.CorruptionType.Betrayal;     return true;
            default:             type = GameManager.CorruptionType.Civilian;     return false;
        }
    }

    private static string Get(string[] fields, int index)
        => index < fields.Length ? fields[index].Trim() : string.Empty;

    // -------------------- CSV Parser --------------------
    // Handles RFC 4180 ("" escaped quotes) and backslash-escaped quotes (\")
    // within double-quoted fields, and multiline quoted cells.

    private static List<string[]> ParseCSV(string text)
    {
        var rows   = new List<string[]>();
        var fields = new List<string>();
        var cur    = new System.Text.StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '\\' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cur.Append('"');
                    i += 2;
                    continue;
                }
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cur.Append('"');
                    i += 2;
                    continue;
                }
                if (c == '"')
                {
                    inQuotes = false;
                    i++;
                    continue;
                }
                cur.Append(c);
                i++;
            }
            else
            {
                if (c == '"')  { inQuotes = true; i++; continue; }
                if (c == ',')  { fields.Add(cur.ToString()); cur.Clear(); i++; continue; }
                if (c == '\r') { i++; continue; }
                if (c == '\n')
                {
                    fields.Add(cur.ToString());
                    cur.Clear();
                    if (fields.Count > 0)
                        rows.Add(fields.ToArray());
                    fields.Clear();
                    i++;
                    continue;
                }
                cur.Append(c);
                i++;
            }
        }

        if (cur.Length > 0 || fields.Count > 0)
        {
            fields.Add(cur.ToString());
            if (fields.Count > 0)
                rows.Add(fields.ToArray());
        }

        return rows;
    }
}
