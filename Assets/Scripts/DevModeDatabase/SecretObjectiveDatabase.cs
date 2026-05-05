using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads secret objectives from a CSV TextAsset (Assets/Database/Corruptions.csv).
/// Expected columns: id, points, Pack, Type, Title, Description, Short Description,
///                   Title (IS), Description (IS), Short Description (IS)
/// Rows with an empty Title are skipped (incomplete data).
/// </summary>
public class SecretObjectiveDatabase : MonoBehaviour
{
    [Tooltip("Drag Assets/Database/Corruptions.csv here.")]
    [SerializeField] private TextAsset corruptionsCSV;

    public List<SecretObjective> LoadDevSecretObjectives()
    {
        var objectives = new List<SecretObjective>();

        if (corruptionsCSV == null)
        {
            Debug.LogError("SecretObjectiveDatabase: corruptionsCSV is not assigned. No objectives loaded.");
            return objectives;
        }

        var rows   = ParseCSV(corruptionsCSV.text);
        int nextId = 0;

        foreach (var fields in rows)
        {
            if (fields.Length < 5) continue;

            // Skip header rows
            string firstField = Get(fields, 0);
            if (firstField == "id" || firstField == "Properties") continue;

            string title = Get(fields, 4);
            if (string.IsNullOrWhiteSpace(title)) continue;

            string typeStr = Get(fields, 3);
            if (!TryParseObjectiveType(typeStr, out GameManager.SecretObjectiveType type)) continue;

            if (!int.TryParse(Get(fields, 1), out int points) || points <= 0)
                points = 60; // sensible fallback

            if (!int.TryParse(firstField, out int id))
                id = nextId;
            nextId = id + 1;

            objectives.Add(new SecretObjective
            {
                id               = id,
                title            = title,
                description      = Get(fields, 5),
                shortDescription = Get(fields, 6),
                points           = points,
                type             = type,
                assignedPlayerId = -1,
                neededCount      = null,
                achievedCount    = null,
                completeted      = false,
            });
        }

        Debug.Log($"SecretObjectiveDatabase: Loaded {objectives.Count} secret objectives from CSV.");
        return objectives;
    }

    // -------------------- Parsing Helpers --------------------

    private static bool TryParseObjectiveType(string s, out GameManager.SecretObjectiveType type)
    {
        switch (s.Trim())
        {
            case "Speech":       type = GameManager.SecretObjectiveType.Speech;       return true;
            case "Interruption": type = GameManager.SecretObjectiveType.Interruption; return true;
            case "Betrayal":     type = GameManager.SecretObjectiveType.Betrayal;     return true;
            default:             type = GameManager.SecretObjectiveType.Civilian;     return false;
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
