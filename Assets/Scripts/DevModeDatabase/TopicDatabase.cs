using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads topics from a CSV TextAsset (Assets/Database/Topics.csv).
/// Column layout (0-indexed):
///   0=id, 1=Ready 4 Publish, 2=Pack, 3=Length, 4=Description Enska,
///   5=Description Íslenska, 6=This, 7=That, 8=Hitt, 9=Þetta
/// Rows where Ready 4 Publish != TRUE are skipped.
/// Rows with an empty English description are skipped (incomplete data).
/// </summary>
public class TopicDatabase : MonoBehaviour
{
    [Tooltip("Drag Assets/Database/Topics.csv here.")]
    [SerializeField] private TextAsset topicsCSV;

    public List<Topic> LoadTopics()
    {
        var topics = new List<Topic>();

        if (topicsCSV == null)
        {
            Debug.LogError("TopicDatabase: topicsCSV is not assigned. No topics loaded.");
            return topics;
        }

        var rows = ParseCSV(topicsCSV.text);
        int nextId = 0;

        foreach (var fields in rows)
        {
            // Skip header rows and rows that are too short
            if (fields.Length < 4) continue;
            if (Get(fields, 0) == "id") continue; // column-header row

            // Skip rows not marked as ready to publish
            string readyStr = Get(fields, 1);
            if (!string.Equals(readyStr, "TRUE", System.StringComparison.OrdinalIgnoreCase)) continue;

            string packStr = Get(fields, 2);
            string typeStr = Get(fields, 3);
            string descEn  = Get(fields, 4);

            // Skip rows with missing required fields
            if (string.IsNullOrWhiteSpace(descEn))  continue;
            if (string.IsNullOrWhiteSpace(packStr))  continue;
            if (string.IsNullOrWhiteSpace(typeStr))  continue;

            if (!TryParsePack(packStr, out GameManager.Pack pack))            continue;
            if (!TryParseTopicType(typeStr, out TopicManager.TopicType type)) continue;

            // Parse optional id; fall back to auto-increment
            if (!int.TryParse(Get(fields, 0), out int id))
                id = nextId;
            nextId = id + 1;

            topics.Add(new Topic
            {
                id            = id,
                pack          = pack,
                type          = type,
                description   = descEn,
                descriptionIs = Get(fields, 5),
                optionA       = Get(fields, 6),
                optionB       = Get(fields, 7),
                optionAIs     = Get(fields, 8),
                optionBIs     = Get(fields, 9),
                seriousness   = 2,
            });
        }

        Debug.Log($"TopicDatabase: Loaded {topics.Count} topics from CSV.");
        return topics;
    }

    // -------------------- Parsing Helpers --------------------

    private static bool TryParsePack(string s, out GameManager.Pack pack)
    {
        switch (s.Trim())
        {
            case "General":      pack = GameManager.Pack.Default;     return true;
            case "Icelandic":    pack = GameManager.Pack.Icelandic;   return true;
            case "EighteenPlus": pack = GameManager.Pack.EighteenPlus;return true;
            case "Political":    pack = GameManager.Pack.Political;    return true;
            case "PopCulture":   pack = GameManager.Pack.PopCulture;   return true;
            default:             pack = GameManager.Pack.Default;     return false;
        }
    }

    private static bool TryParseTopicType(string s, out TopicManager.TopicType type)
    {
        switch (s.Trim())
        {
            case "This or That": type = TopicManager.TopicType.Versus;    return true;
            case "Scenarios":    type = TopicManager.TopicType.Scenarios; return true;
            default:             type = TopicManager.TopicType.Versus;    return false;
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
                // Backslash-escaped quote: \"
                if (c == '\\' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cur.Append('"');
                    i += 2;
                    continue;
                }
                // RFC 4180 double-quote escape: ""
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cur.Append('"');
                    i += 2;
                    continue;
                }
                // Closing quote
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
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                    continue;
                }
                if (c == ',')
                {
                    fields.Add(cur.ToString());
                    cur.Clear();
                    i++;
                    continue;
                }
                if (c == '\r')
                {
                    i++;
                    continue; // skip CR; LF handled below
                }
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

        // Flush any trailing content
        if (cur.Length > 0 || fields.Count > 0)
        {
            fields.Add(cur.ToString());
            if (fields.Count > 0)
                rows.Add(fields.ToArray());
        }

        return rows;
    }
}
