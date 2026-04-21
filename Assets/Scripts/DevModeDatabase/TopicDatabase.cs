using System.Collections.Generic;
using UnityEngine;

public class TopicDatabase : MonoBehaviour
{
    private List<Topic> topics;

    public List<Topic> LoadDevTopics()
    {
        int id = 0;
        topics = new List<Topic>();

        id = AddDefaultTopics(id);
        id = AddIcelandicTopics(id);
        id = AddEighteenPlusTopics(id);
        id = AddPoliticalTopics(id);
        id = AddPopCultureTopics(id);

        Debug.Log($"TopicDatabase: Prepared {topics.Count} dev topics.");
        return topics;
    }

    // -------------------- Pack: Default --------------------

    private int AddDefaultTopics(int startId)
    {
        var pack = GameManager.Pack.Default;
        int id = startId;

        // Short (5)
        id = AddTopic(id, "Couch Potato", "Playing videogames is a waste of time.", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Cat / Dog", "Cats make better pets than dogs", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Carrots / Sticks", "Teachers should be strict", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Flatulence", "Fart jokes are hilarious", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Hot Dogs / Kebab", "Hotdogs are better than kebabs after the club", pack, TopicManager.TopicType.Short);

        // Medium (6)
        id = AddTopic(id, "Taxi Tip", "My taxi driver let me choose the music, but also vaped the entire time. Does he deserve a tip?", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Flying Fragrance", "Strong fragrances should be catigoricaly banned on all flights", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Drramaaaa", "Drama classes should be mandatory in all elementary schools to encourage emotional expression", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Reply All", "Replying-all to a company-wide email should be a fireable offense", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "The Arts", "Online influencers should receive government supplied artist salaries from the state", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Hosting Etiquette", "It is acceptable to only offer beer at a social gathering that advertises free drinks.", pack, TopicManager.TopicType.Medium);

        // Long (5)
        id = AddTopic(id, "Shower Debate", "Your roommate takes 45-minute showers every morning and uses all the hot water, but they also cook dinner for you every night. Do you confront them about the showers?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Birthday Rule", "Your friend always forgets your birthday but throws themselves a massive party every year and expects a gift. Do you stop buying them presents or keep the peace?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Fridge Thief", "Someone at work keeps eating your clearly labeled lunch from the fridge. HR says there's nothing they can do. Is it ethical to put an absurd amount of hot sauce in your food as a trap?",  pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Queue Justice", "You've been waiting in line for 20 minutes and someone cuts in front of you claiming they were 'holding a spot.' The rest of the line does nothing. Do you make a scene?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Nudist Cults", "The cops have been arresting members of a nudist cult in Vestmanneyjar. Should we allow religious exemptions for public nudity?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Brainrot", "Kids spend hours every day scrolling brainrot online. We should implement restrictions preventing them from accessing social media until they're 18.", pack, TopicManager.TopicType.Long);

        return id;
    }

    // -------------------- Pack: Icelandic --------------------

    private int AddIcelandicTopics(int startId)
    {
        var pack = GameManager.Pack.Icelandic;
        int id = startId;

        id = AddTopic(id, "Hot Dog Best", "Is the Icelandic hot dog really the best hot dog in the world?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Elf Roads", "Should we reroute highways to avoid disturbing elf habitations?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Puffin Cute", "Are puffins overrated?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Name Committee", "Is the Icelandic Naming Committee a good idea?", pack, TopicManager.TopicType.Short);

        id = AddTopic(id, "Pool Rules", "Icelanders must shower naked before entering public pools. Should tourists get a pass?", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Shark Snack", "Fermented shark is offered at a dinner party. Is it rude to refuse?", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Darkness Wins", "Is 22 hours of winter darkness cozy or a human rights violation?", pack, TopicManager.TopicType.Medium);

        id = AddTopic(id, "Volcano Tourism", "A volcano is erupting near Reykjavik. Thousands of tourists flock to see it, blocking rescue routes. Should volcano tourism be banned during active eruptions?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Surname Chaos", "Iceland uses patronymic surnames so siblings can have different last names. A tourist is confused and calls the system stupid on social media. Do they owe Iceland an apology?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Wool War", "Your friend bought a knockoff Icelandic sweater online for a tenth of the price. Is it okay to wear it in Reykjavik or is that disrespectful to Icelandic wool heritage?", pack, TopicManager.TopicType.Long);

        return id;
    }

    // -------------------- Pack: 18+ --------------------

    private int AddEighteenPlusTopics(int startId)
    {
        var pack = GameManager.Pack.EighteenPlus;
        int id = startId;

        id = AddTopic(id, "Drunk Text", "Is drunk texting your ex ever justified?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Walk of Shame", "Walk of shame or stride of pride?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Hangover Cure", "Hair of the dog: genius or self-destruction?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Bar Close", "Should bars close at 2am or stay open until sunrise?", pack, TopicManager.TopicType.Short);

        id = AddTopic(id, "Tab Etiquette", "Your date orders the most expensive cocktail on the menu and then suggests splitting the bill. Fair or foul?", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Party Foul", "You accidentally break a lamp at a house party. Do you confess, secretly replace it, or pretend nothing happened?", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Karaoke Law", "Should there be a legal limit on how many times someone can sing Bohemian Rhapsody at karaoke?", pack, TopicManager.TopicType.Medium);

        id = AddTopic(id, "Festival Dilemma", "Your group buys VIP festival tickets but one friend can only afford general admission. They sneak into VIP and get your whole group kicked out. Who's in the wrong?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Uber Confession", "Your uber driver starts telling you their entire life story including deeply personal details. Do you listen politely, put in headphones, or give life advice?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Brunch Betrayal", "Your friend posts an Instagram story of your brunch hangover face without permission and it goes semi-viral. Is this a friendship-ending offense?", pack, TopicManager.TopicType.Long);

        return id;
    }

    // -------------------- Pack: Political --------------------

    private int AddPoliticalTopics(int startId)
    {
        var pack = GameManager.Pack.Political;
        int id = startId;

        id = AddTopic(id, "Voting Age", "Should the voting age be lowered to 16?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Term Limits", "Should all politicians have term limits?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Flag Design", "Does your country's flag need a redesign?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Nap Policy", "Should mandatory nap time be a workplace law?", pack, TopicManager.TopicType.Short);

        id = AddTopic(id, "Pet Mayor", "A town elected a dog as mayor and things are going surprisingly well. Should animals be allowed to hold public office?", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Dress Code", "Should politicians be required to wear silly hats during parliamentary debates to lighten the mood?", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Moon Embassy", "Should we establish an embassy on the Moon before any country claims it?", pack, TopicManager.TopicType.Medium);

        id = AddTopic(id, "Pigeon Rights", "Pigeons outnumber humans in most major cities. A radical party proposes giving pigeons voting rights proportional to their population. Is this democracy or chaos?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Time Zone War", "A small country on the border of two time zones can't agree which one to use. Half the country shows up to work an hour early, the other half an hour late. How do you solve this?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "AI President", "An AI is polling better than all human candidates in a presidential race. It promises zero corruption and optimal policy decisions. Should an AI be allowed to run for president?", pack, TopicManager.TopicType.Long);

        return id;
    }

    // -------------------- Pack: Pop Culture --------------------

    private int AddPopCultureTopics(int startId)
    {
        var pack = GameManager.Pack.PopCulture;
        int id = startId;

        id = AddTopic(id, "Reboot Madness", "Should Hollywood stop making reboots?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Autotune Ban", "Should autotune be banned from music?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "Spoiler Alert", "Is it your fault if you get spoiled on a show that aired last week?", pack, TopicManager.TopicType.Short);
        id = AddTopic(id, "GIF War", "Is it pronounced GIF or JIF?", pack, TopicManager.TopicType.Short);

        id = AddTopic(id, "Binge vs Weekly", "Is binge-releasing an entire season better than weekly episodes?", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Nepo Babies", "Should nepo babies be required to disclose their parents on their resumes?", pack, TopicManager.TopicType.Medium);
        id = AddTopic(id, "Concert Phones", "Should phones be banned at live concerts?", pack, TopicManager.TopicType.Medium);

        id = AddTopic(id, "Cinematic Universe", "Your friend insists you need to watch 47 movies in chronological order before seeing the latest superhero film. You just want to watch one movie. Who is right?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Stan Culture", "A celebrity's fan base doxxes a food critic who gave their idol's restaurant a bad review. Is the critic obligated to change the review for their own safety?", pack, TopicManager.TopicType.Long);
        id = AddTopic(id, "Reality TV Law", "A reality TV show contestant claims they were edited to look like a villain. Should reality shows be legally required to show unedited footage?", pack, TopicManager.TopicType.Long);

        return id;
    }

    // -------------------- Helper --------------------

    private int AddTopic(int id, string title, string description, GameManager.Pack pack, TopicManager.TopicType type, int seriousness = 2, string leadingQuestionFor = "", string leadingQuestionAgainst = "")
    {
        topics.Add(new Topic
        {
            id = id,
            title = title,
            description = description,
            pack = pack,
            type = type,
            seriousness = seriousness,
            leadingQuestionFor = leadingQuestionFor,
            leadingQuestionAgainst = leadingQuestionAgainst
        });
        return id + 1;
    }
}