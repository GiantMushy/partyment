using System.Collections.Generic;
using UnityEngine;

public class SecretObjectiveDatabase : MonoBehaviour
{
    private List<SecretObjective> objectives;

    public List<SecretObjective> LoadDevSecretObjectives()
    {
        int id = 0;
        objectives = new List<SecretObjective>();

        id = AddSpeechObjectives(id);
        id = AddInterruptionObjectives(id);
        id = AddBetrayalObjectives(id);

        Debug.Log($"SecretObjectiveDatabase: Prepared {objectives.Count} dev secret objectives.");
        return objectives;
    }

    // -------------------- Speech Objectives --------------------

    private int AddSpeechObjectives(int startId)
    {
        var type = GameManager.SecretObjectiveType.Speech;
        int id = startId;

        id = AddObjective(id, "Nickelback Fan",
            "Reference \"Nickelback\" at any point during your speech.",
            "Mention Nickelback", type);

        id = AddObjective(id, "Conspiracy Theorist",
            "Work a conspiracy theory into your argument as if it were a proven fact.",
            "Use a conspiracy theory", type);

        id = AddObjective(id, "Movie Buff",
            "Quote a famous movie line during your speech without anyone calling you out.",
            "Quote a movie", type);

        id = AddObjective(id, "Poet Laureate",
            "Rhyme at least two sentences in a row during your speech.",
            "Rhyme two sentences", type);

        id = AddObjective(id, "Name Dropper",
            "Mention a celebrity by name as if you personally know them.",
            "Name drop a celebrity", type);

        id = AddObjective(id, "Statistician",
            "Make up a completely fake statistic and present it with total confidence.",
            "Fake a statistic", type);

        id = AddObjective(id, "Time Traveler",
            "Reference an event from the future as if it already happened.",
            "Reference the future", type);

        id = AddObjective(id, "Foreign Diplomat",
            "Use a word or phrase from another language and pretend it's common knowledge.",
            "Use a foreign phrase", type);

        id = AddObjective(id, "Dramatic Pause",
            "Take an uncomfortably long dramatic pause (at least 5 seconds) mid-sentence.",
            "Long dramatic pause", type);

        id = AddObjective(id, "Catchphrase King",
            "Say the phrase \"and that's a fact\" at least three times during your speech.",
            "Say catchphrase 3x", type);

        return id;
    }

    // -------------------- Interruption Objectives --------------------

    private int AddInterruptionObjectives(int startId)
    {
        var type = GameManager.SecretObjectiveType.Interruption;
        int id = startId;

        id = AddObjective(id, "Slow Clap Starter",
            "Start a group clap during someone else's speech.",
            "Start a group clap", type);

        id = AddObjective(id, "Fact Checker",
            "Loudly say \"Actually...\" and interrupt someone to correct a minor detail.",
            "Interrupt with \"Actually...\"", type);

        id = AddObjective(id, "Standing Ovation",
            "Stand up and applaud enthusiastically at an inappropriate moment during someone else's speech.",
            "Applaud inappropriately", type);

        id = AddObjective(id, "Phone a Friend",
            "Pretend to receive an important phone call during someone else's argument.",
            "Fake a phone call", type);

        id = AddObjective(id, "Heckler",
            "Boo or thumbs-down another group's argument at least once.",
            "Boo an argument", type);

        id = AddObjective(id, "Point of Order",
            "Yell \"Point of order!\" during someone else's speech and raise a completely irrelevant objection.",
            "Yell Point of order", type);

        id = AddObjective(id, "Sneeze Attack",
            "Have a loud, dramatic fake sneezing fit during someone else's key argument.",
            "Fake sneeze fit", type);

        id = AddObjective(id, "Echo Chamber",
            "Repeat the last word of someone else's sentence loudly, at least twice during the round.",
            "Echo someone's words", type);

        id = AddObjective(id, "The Narrator",
            "Narrate someone else's actions out loud in a nature documentary voice.",
            "Narrate like a documentary", type);

        id = AddObjective(id, "Question Time",
            "Interrupt someone's speech to ask them a completely unrelated question.",
            "Ask an unrelated question", type);

        return id;
    }

    // -------------------- Betrayal Objectives --------------------

    private int AddBetrayalObjectives(int startId)
    {
        var type = GameManager.SecretObjectiveType.Betrayal;
        int id = startId;

        id = AddObjective(id, "Double Agent",
            "Sabotage your group's speech so that it gets zero votes this round.",
            "Sabotage your group", type);

        id = AddObjective(id, "Devil's Advocate",
            "Secretly argue FOR the opposing side during your own group's speech.",
            "Argue for the other side", type);

        id = AddObjective(id, "The Fumble",
            "Deliberately forget your group's main argument mid-speech and improvise something terrible.",
            "Forget your argument", type);

        id = AddObjective(id, "Credit Stealer",
            "Take credit for an idea that came from another group's speech.",
            "Steal another group's idea", type);

        id = AddObjective(id, "Confidence Killer",
            "Subtly undermine your teammate by saying \"Well, they tried\" after they speak.",
            "Undermine your teammate", type);

        id = AddObjective(id, "Wrong Side",
            "Start your part of the speech by accidentally arguing for the wrong side before \"correcting\" yourself.",
            "Argue the wrong side first", type);

        id = AddObjective(id, "Distracted Speaker",
            "During your group's speech, look completely disinterested and yawn at least twice.",
            "Look bored during speech", type);

        id = AddObjective(id, "Contradiction Machine",
            "Contradict something your teammate just said and insist you're both on the same page.",
            "Contradict your teammate", type);

        id = AddObjective(id, "TMI",
            "Derail your group's speech with an overly personal and irrelevant anecdote.",
            "Tell an irrelevant story", type);

        id = AddObjective(id, "Apology Tour",
            "Apologize to the opposing side mid-speech and say they actually make a good point.",
            "Apologize to opponents", type);

        return id;
    }

    // -------------------- Helper --------------------

    private int AddObjective(int id, string title, string description, string shortDescription, GameManager.SecretObjectiveType type)
    {
        objectives.Add(new SecretObjective
        {
            id = id,
            title = title,
            description = description,
            shortDescription = shortDescription,
            points = Random.Range(2, 11) * 10, // Random points: 20, 30, 40, ... 100
            type = type,
            assignedPlayerId = -1,
            neededCount = null,
            achievedCount = null,
            completeted = false
        });
        return id + 1;
    }
}
