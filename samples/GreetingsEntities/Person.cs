namespace GreetingsEntities;

internal class Person
{
    private int _id { get; set; }
    private readonly List<Greeting> _greetings = new List<Greeting>();
    public byte[] TimeStamp { get; private set; }
    public string Name { get; private set; }
    public IReadOnlyList<Greeting> Greetings => _greetings;

    public Person(string name)
    {
        Name = name;
    }

    public Person(int id, string name)
    {
        _id = id;
        Name = name;
    }

    public void AddGreeting(Greeting greeting)
    {
        greeting.Recipient = this;
        _greetings.Add(greeting);
    }
}
