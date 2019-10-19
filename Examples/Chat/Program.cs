using System;
using Newtonsoft.Json.Linq;
using SharpExchange.Auth;
using SharpExchange.Chat.Events;
using SharpExchange.Chat.Events.User.Extensions;
using SharpExchange.Net.WebSockets;
using SharpExchange.Chat.Actions;
using System.Threading.Tasks;
using SharpExchange.Chat.Events.User;

public class AllData : ChatEventDataProcessor, IChatEventHandler<string>
{
	// The type of event we want to process.
	public override EventType Event => EventType.All;

	public event Action<string> OnEvent;

	// Process the incoming JSON data coming from the RoomWatcher's
	// WebSocket. In this example, we just stringify the object and
	// invoke any listeners.
	public override void ProcessEventData(JToken data) => OnEvent?.Invoke(data.ToString());
}

public class Program
{
	// This stuff should ideally be loaded in from a configuration provider.
	private const string roomUrl = "https://chat.stackexchange.com/rooms/1/sandbox";

	private static void Main(string[] args) => Demo().Wait();

	private static async Task Demo()
	{
		// Fetch your account's credentials from somewhere.
		Console.WriteLine("Username:");
		string username = Console.ReadLine();
		Console.WriteLine("Password:");

		var auth = new EmailAuthenticationProvider(username, Console.ReadLine());
		Console.Clear();

		while (true)
		{
			try
			{
				using var _ = new RoomWatcher<DefaultWebSocket>(auth, roomUrl);
				break;
			}
			catch (InvalidCredentialsException e)
			{
				Console.WriteLine("Wrong login credentials");
				Console.WriteLine("Username:");
				username = Console.ReadLine();
				Console.WriteLine("Password:");
				auth = new EmailAuthenticationProvider(username, Console.ReadLine());
				Console.Clear();
			}
		}
		// Create an instance of the ActionScheduler. This will
		// allow us to execute chat actions like: posting messages,
		// kicking users, moving messages, etc.
		using var actionScheduler = new ActionScheduler(auth, roomUrl);
		// Create an instance of the RoomWatcher class. Here we
		// specify (via the type parameter) what WebSocket implementation
		// we'd like to use. This class allows you to subscribe to chat events.
		using var roomWatcher = new RoomWatcher<DefaultWebSocket>(auth, roomUrl);

		async void HandleUserMentioned(MentionedUser obj)
		{
			_ = await actionScheduler.CreateMessageAsync("Hi, I was pinged by id " + obj.PingerId);
		}
		// Subscribe to the UserMentioned event.
		_ = roomWatcher.AddUserMentionedEventHandler(HandleUserMentioned);

		// Besides being able to subscribe to the default events,
		// you can also create (and listen to) your own. Your class must
		// implement the ChatEventDataProcessor class, you can also
		// optionally implement IChatEventHandler or IChatEventHandler<T>.
		var customEventHanlder = new AllData();

		// Add a very basic handler.
		customEventHanlder.OnEvent += data => Console.WriteLine("Eventdata: " + data);

		// Add our custom event handler so we can
		// begin processing the incoming event data.
		roomWatcher.EventRouter.AddProcessor(customEventHanlder);

		// Post a simple message.
		var messageId = await actionScheduler.CreateMessageAsync("Hello world.");

		while (Console.ReadKey(true).Key != ConsoleKey.Q)
		{

		}
	}
}