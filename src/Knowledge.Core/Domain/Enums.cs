namespace Knowledge.Core.Domain;

public enum CandidateStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum MessageRole
{
    System = 0,
    User = 1,
    Assistant = 2
}

public enum IntentType
{
    Question = 0,
    Conversation = 1,
    Feedback = 2,
    NewKnowledge = 3,
    Correction = 4
}
