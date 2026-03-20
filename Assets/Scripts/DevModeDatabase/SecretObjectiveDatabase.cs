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
            "Mention Nickelback", type, 40);

        id = AddObjective(id, "Conspiracy Theorist",
            "Work a conspiracy theory into your argument as if it were a proven fact.",
            "Use a conspiracy theory", type, 60);

        id = AddObjective(id, "Movie Buff",
            "Quote a famous movie line during your speech without anyone calling you out.",
            "Quote a movie", type, 60);

        id = AddObjective(id, "Poet Laureate",
            "Rhyme at least two sentences in a row during your speech.",
            "Rhyme two sentences", type, 100);

        id = AddObjective(id, "Name Dropper",
            "Mention a celebrity by name as if you personally know them.",
            "Name drop a celebrity", type, 60);

        id = AddObjective(id, "Statistician",
            "Make up a completely fake statistic and present it with total confidence.",
            "Fake a statistic", type, 60);

        id = AddObjective(id, "Photosynthesis",
            "Confidently use a word or phrase in a completly incorrect context.",
            "Use a word incorrectly", type, 60);

        id = AddObjective(id, "Dramatic Pause",
            "Take an uncomfortably long dramatic pause (at least 5 seconds) mid-sentence.",
            "Long dramatic pause", type, 80);

        id = AddObjective(id, "Catchphrase King",
            "Say the phrase \"and that's a fact\" at least three times during your speech.",
            "Say catchphrase 3x", type, 80);

        id = AddObjective(id, "Belieber",
            "Reference a Justin Bieber lyric in your speech.",
            "Reference a Bieber lyric", type, 40);

        id = AddObjective(id, "The Countdown",
            "Say \"1\", \"2\", \"3\", \"4\" & \"5\" at some point during your speech. Does not have to be in order.",
            "Say 1 through 5", type, 80);

        id = AddObjective(id, "Old MacDonald",
            "Make a dog, cat, duck, and cow noise at least once for each during your speech.",
            "Make 4 animal noises", type, 120);
 
        id = AddObjective(id, "Iceguys Superfan",
            "Reference Iceguys during your speech.",
            "Reference Iceguys", type, 40);
        return id;
    }

    // -------------------- Interruption Objectives --------------------

    private int AddInterruptionObjectives(int startId)
    {
        var type = GameManager.SecretObjectiveType.Interruption;
        int id = startId;

        id = AddObjective(id, "Slow Clap Starter",
            "Start a group clap during or right after someone else's speech. At least 1 person from an opposing group must join in.",
            "Start a group clap", type, 140);

        id = AddObjective(id, "Fact Checker",
            "Loudly say \"Actually...\" and interrupt someone to correct a minor detail. Your correction does not have to be correct.",
            "Interrupt with \"Actually...\"", type, 100);

        id = AddObjective(id, "Standing Ovation",
            "Stand up and applaud enthusiastically at any moment during someone else's speech.",
            "Applaud enthusiastically", type, 120);

        id = AddObjective(id, "Phone a Friend",
            "Pretend to receive an important phone call during someone else's argument.",
            "Fake a phone call", type, 100);

        id = AddObjective(id, "Heckler",
            "Boo or thumbs-down another group's argument at least once.",
            "Boo an argument", type, 80);
        id = AddObjective(id, "Sneeze Attack",
            "Have a loud, dramatic fake sneezing fit during someone else's key argument.",
            "Fake sneeze fit", type, 100);

        id = AddObjective(id, "Echo Chamber",
            "Repeat the last word of someone else's sentence loudly, at least twice during the round.",
            "Echo someone's words", type, 120);

        id = AddObjective(id, "Question Time",
            "Interrupt someone's speech to ask them a completely unrelated question.",
            "Ask an unrelated question", type, 100);

        id = AddObjective(id, "Stand Up Act",
            "Have someone stand up during your speech.",
            "Get someone to stand up", type, 150);

        id = AddObjective(id, "Opening Yawn",
            "Yawn loudly the moment someone else starts their speech.",
            "Yawn at speech start", type, 80);

        id = AddObjective(id, "Five Claps",
            "Clap 5 times.",
            "Clap 5 times", type, 80);

        return id;
    }

    // -------------------- Betrayal Objectives --------------------

    private int AddBetrayalObjectives(int startId)
    {
        var type = GameManager.SecretObjectiveType.Betrayal;
        int id = startId;

        id = AddObjective(id, "Double Agent",
            "Sabotage your group's speech so that it gets zero votes this round.",
            "Sabotage your group", type, 200);

        id = AddObjective(id, "Devil's Advocate",
            "Secretly argue FOR the opposing side during your own group's speech.",
            "Argue for the other side", type, 180);

        id = AddObjective(id, "The Fumble",
            "Deliberately forget your group's main argument mid-speech and improvise something terrible.",
            "Forget your argument", type, 160);

        id = AddObjective(id, "Credit Stealer",
            "Take credit for an idea that came from another group's speech. The points are void if the original group calls you out on it.",
            "Steal another group's idea", type, 160);

        id = AddObjective(id, "Confidence Killer",
            "Subtly undermine your teammate by saying \"Well, they tried\" after they speak.",
            "Undermine your teammate", type, 160);

        id = AddObjective(id, "Wrong Side",
            "Start your part of the speech by accidentally arguing for the wrong side before \"correcting\" yourself.",
            "Argue the wrong side first", type, 160);

        id = AddObjective(id, "Contradiction Machine",
            "Contradict something your teammate just said and insist you're both on the same page.",
            "Contradict your teammate", type, 180);

        id = AddObjective(id, "TMI",
            "Derail your group's speech with an overly personal and irrelevant anecdote.",
            "Tell an irrelevant story", type, 160);

        id = AddObjective(id, "Apology Tour",
            "Apologize to the opposing side mid-speech and say they actually make a good point.",
            "Apologize to opponents", type, 190);

        return id;
    }

    // -------------------- Helper --------------------

    private int AddObjective(int id, string title, string description, string shortDescription, GameManager.SecretObjectiveType type, int points = -1)
    {
        if (points == -1)
        {
            points = Random.Range(2, 11) * 20; // Random points: 40, 60, 80, ... 200
        }
        objectives.Add(new SecretObjective
        {
            id = id,
            title = title,
            description = description,
            shortDescription = shortDescription,
            points = points,
            type = type,
            assignedPlayerId = -1,
            neededCount = null,
            achievedCount = null,
            completeted = false
        });
        return id + 1;
    }
}
