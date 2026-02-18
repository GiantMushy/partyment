using System.Collections.Generic;
using UnityEngine;

public class BillDatabase : MonoBehaviour
{
    private List<Bill> bills;

    public List<Bill> LoadDevBills()
    {
        int id = 0;
        bills = new List<Bill>();

        id = AddDefaultBills(id);
        id = AddIcelandicBills(id);
        id = AddEighteenPlusBills(id);
        id = AddPoliticalBills(id);
        id = AddPopCultureBills(id);

        Debug.Log($"BillDatabase: Prepared {bills.Count} dev bills.");
        return bills;
    }

    // -------------------- Pack: Default --------------------

    private int AddDefaultBills(int startId)
    {
        var pack = GameManager.Pack.Default;
        int id = startId;

        // Short (4)
        id = AddBill(id, "Coke vs Pepsi", "Coke or Pepsi?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Cat vs Dog", "Cats or dogs?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Morning vs Night", "Are you a morning person or a night owl?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Sweet vs Savory", "Sweet snacks or savory snacks?", pack, BillManager.BillType.Short);

        // Medium (4)
        id = AddBill(id, "Taxi Tip", "My taxi driver let me choose the music, but also vaped the entire time. Does he deserve a tip?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Cereal Water", "Is it acceptable to eat cereal with water if you're out of milk?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Sock Shoes", "Is it ever okay to wear socks with sandals?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Reply All", "Should replying-all to a company-wide email be a fireable offense?", pack, BillManager.BillType.Medium);

        // Long (4)
        id = AddBill(id, "Shower Debate", "Your roommate takes 45-minute showers every morning and uses all the hot water, but they also cook dinner for you every night. Do you confront them about the showers?", pack, BillManager.BillType.Long);
        id = AddBill(id, "Birthday Rule", "Your friend always forgets your birthday but throws themselves a massive party every year and expects a gift. Do you stop buying them presents or keep the peace?", pack, BillManager.BillType.Long);
        id = AddBill(id, "Fridge Thief", "Someone at work keeps eating your clearly labeled lunch from the fridge. HR says there's nothing they can do. Is it ethical to put an absurd amount of hot sauce in your food as a trap?",  pack, BillManager.BillType.Long);
        id = AddBill(id, "Queue Justice", "You've been waiting in line for 20 minutes and someone cuts in front of you claiming they were 'holding a spot.' The rest of the line does nothing. Do you make a scene?", pack, BillManager.BillType.Long);

        return id;
    }

    // -------------------- Pack: Icelandic --------------------

    private int AddIcelandicBills(int startId)
    {
        var pack = GameManager.Pack.Icelandic;
        int id = startId;

        id = AddBill(id, "Hot Dog Best", "Is the Icelandic hot dog really the best hot dog in the world?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Elf Roads", "Should we reroute highways to avoid disturbing elf habitations?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Puffin Cute", "Are puffins overrated?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Name Committee", "Is the Icelandic Naming Committee a good idea?", pack, BillManager.BillType.Short);

        id = AddBill(id, "Pool Rules", "Icelanders must shower naked before entering public pools. Should tourists get a pass?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Shark Snack", "Fermented shark is offered at a dinner party. Is it rude to refuse?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Darkness Wins", "Is 22 hours of winter darkness cozy or a human rights violation?", pack, BillManager.BillType.Medium);

        id = AddBill(id, "Volcano Tourism", "A volcano is erupting near Reykjavik. Thousands of tourists flock to see it, blocking rescue routes. Should volcano tourism be banned during active eruptions?", pack, BillManager.BillType.Long);
        id = AddBill(id, "Surname Chaos", "Iceland uses patronymic surnames so siblings can have different last names. A tourist is confused and calls the system stupid on social media. Do they owe Iceland an apology?", pack, BillManager.BillType.Long);
        id = AddBill(id, "Wool War", "Your friend bought a knockoff Icelandic sweater online for a tenth of the price. Is it okay to wear it in Reykjavik or is that disrespectful to Icelandic wool heritage?", pack, BillManager.BillType.Long);

        return id;
    }

    // -------------------- Pack: 18+ --------------------

    private int AddEighteenPlusBills(int startId)
    {
        var pack = GameManager.Pack.EighteenPlus;
        int id = startId;

        id = AddBill(id, "Drunk Text", "Is drunk texting your ex ever justified?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Walk of Shame", "Walk of shame or stride of pride?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Hangover Cure", "Hair of the dog: genius or self-destruction?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Bar Close", "Should bars close at 2am or stay open until sunrise?", pack, BillManager.BillType.Short);

        id = AddBill(id, "Tab Etiquette", "Your date orders the most expensive cocktail on the menu and then suggests splitting the bill. Fair or foul?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Party Foul", "You accidentally break a lamp at a house party. Do you confess, secretly replace it, or pretend nothing happened?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Karaoke Law", "Should there be a legal limit on how many times someone can sing Bohemian Rhapsody at karaoke?", pack, BillManager.BillType.Medium);

        id = AddBill(id, "Festival Dilemma", "Your group buys VIP festival tickets but one friend can only afford general admission. They sneak into VIP and get your whole group kicked out. Who's in the wrong?", pack, BillManager.BillType.Long);
        id = AddBill(id, "Uber Confession", "Your uber driver starts telling you their entire life story including deeply personal details. Do you listen politely, put in headphones, or give life advice?", pack, BillManager.BillType.Long);
        id = AddBill(id, "Brunch Betrayal", "Your friend posts an Instagram story of your brunch hangover face without permission and it goes semi-viral. Is this a friendship-ending offense?", pack, BillManager.BillType.Long);

        return id;
    }

    // -------------------- Pack: Political --------------------

    private int AddPoliticalBills(int startId)
    {
        var pack = GameManager.Pack.Political;
        int id = startId;

        id = AddBill(id, "Voting Age", "Should the voting age be lowered to 16?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Term Limits", "Should all politicians have term limits?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Flag Design", "Does your country's flag need a redesign?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Nap Policy", "Should mandatory nap time be a workplace law?", pack, BillManager.BillType.Short);

        id = AddBill(id, "Pet Mayor", "A town elected a dog as mayor and things are going surprisingly well. Should animals be allowed to hold public office?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Dress Code", "Should politicians be required to wear silly hats during parliamentary debates to lighten the mood?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Moon Embassy", "Should we establish an embassy on the Moon before any country claims it?", pack, BillManager.BillType.Medium);

        id = AddBill(id, "Pigeon Rights", "Pigeons outnumber humans in most major cities. A radical party proposes giving pigeons voting rights proportional to their population. Is this democracy or chaos?", pack, BillManager.BillType.Long);
        id = AddBill(id, "Time Zone War", "A small country on the border of two time zones can't agree which one to use. Half the country shows up to work an hour early, the other half an hour late. How do you solve this?", pack, BillManager.BillType.Long);
        id = AddBill(id, "AI President", "An AI is polling better than all human candidates in a presidential race. It promises zero corruption and optimal policy decisions. Should an AI be allowed to run for president?", pack, BillManager.BillType.Long);

        return id;
    }

    // -------------------- Pack: Pop Culture --------------------

    private int AddPopCultureBills(int startId)
    {
        var pack = GameManager.Pack.PopCulture;
        int id = startId;

        id = AddBill(id, "Reboot Madness", "Should Hollywood stop making reboots?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Autotune Ban", "Should autotune be banned from music?", pack, BillManager.BillType.Short);
        id = AddBill(id, "Spoiler Alert", "Is it your fault if you get spoiled on a show that aired last week?", pack, BillManager.BillType.Short);
        id = AddBill(id, "GIF War", "Is it pronounced GIF or JIF?", pack, BillManager.BillType.Short);

        id = AddBill(id, "Binge vs Weekly", "Is binge-releasing an entire season better than weekly episodes?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Nepo Babies", "Should nepo babies be required to disclose their parents on their resumes?", pack, BillManager.BillType.Medium);
        id = AddBill(id, "Concert Phones", "Should phones be banned at live concerts?", pack, BillManager.BillType.Medium);

        id = AddBill(id, "Cinematic Universe", "Your friend insists you need to watch 47 movies in chronological order before seeing the latest superhero film. You just want to watch one movie. Who is right?", pack, BillManager.BillType.Long);
        id = AddBill(id, "Stan Culture", "A celebrity's fan base doxxes a food critic who gave their idol's restaurant a bad review. Is the critic obligated to change the review for their own safety?", pack, BillManager.BillType.Long);
        id = AddBill(id, "Reality TV Law", "A reality TV show contestant claims they were edited to look like a villain. Should reality shows be legally required to show unedited footage?", pack, BillManager.BillType.Long);

        return id;
    }

    // -------------------- Helper --------------------

    private int AddBill(int id, string title, string description, GameManager.Pack pack, BillManager.BillType type)
    {
        bills.Add(new Bill
        {
            id = id,
            title = title,
            description = description,
            pack = pack,
            type = type,
            seriousness = 2,
            leadingQuestionFor = "",
            leadingQuestionAgainst = ""
        });
        return id + 1;
    }
}