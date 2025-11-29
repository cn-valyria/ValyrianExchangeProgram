namespace Valyria.Core.DTOs.CN;

public enum GovernmentType
    {
        Anarchy = 1,
        Capitalist = 2,
        Communist = 3,
        Democracy = 4,
        Dictatorship = 5,
        FederalGovernment = 6,
        Monarchy = 7,
        Republic = 8,
        RevolutionaryGov = 9,
        TotalitarianState = 10,
        Transitional = 11
    }

    public enum Religion
    {
        None = 1,
        Mixed = 2,
        BahaiFaith = 3,
        Buddhism = 4,
        Christianity = 5,
        Confucianism = 6,
        Hinduism = 7,
        Islam = 8,
        Jainism = 9,
        Judaism = 10,
        Norse = 11,
        Shinto = 12,
        Sikhism = 13,
        Taoism = 14,
        Voodoo = 15
    }

    public enum Team
    {
        Aqua = 1,
        White = 2,
        Red = 3,
        Green = 4,
        Brown = 5,
        Blue = 6,
        Purple = 7,
        Orange = 8,
        Maroon = 9,
        Black = 10,
        Yellow = 11,
        Pink = 12,
        None = 13
    }

    public enum NationalWarStatus
    {
        War = 1,
        Peace = 2
    }

    public enum RecentActivity
    {
        ActiveInTheLast3Days = 1,
        ActiveThisWeek = 2,
        ActiveLastWeek = 3,
        ActiveThreeWeeksAgo = 4,
        ActiveMoreThanThreeWeeksAgo = 5
    }

    public enum WarStatus
    {
        Active = 1,
        Expired = 2,
        Peace = 3
    }

    public enum AidStatus
    {
        Pending = 1,
        Approved = 2,
        Cancled = 3, // Yes this is intentionally misspelled; admin is dumb and can't spell apparently
        Expired = 4
    }
