using System;
using System.Threading;
using System.Threading.Tasks;
using static KopiLua.Lua;

// this example shows how to run the Lua VM in its own Task so that it doesn't tie up the main GUI thread.
// it also demonstrates task cancellation and simple integration with asynchronous programming.
// please refer to the KopiLua help file for more details.
namespace Example3
{
	public class Program
	{
		private static CancellationTokenSource CancelSource;

		static void Main()
		{
			// start the Lua VM task
			CancelSource = new CancellationTokenSource();
			var vmTask = Task.Run(() => RunVM(), CancelSource.Token);

			// Wait for a keypress
			Console.Write("VM is running, press any key to stop");
			Console.ReadKey();

			// cancel the task and wait for it to finish
			CancelSource.Cancel();
			vmTask.Wait();
		}

		static string lua_script =
			"while true do		" +
			"	io.write(\".\")	" +
			"	delay(1000)		" +
			"end";

		static void RunVM()
		{
			lua_State L = null;

			try
			{
				// initialization
				L = lua_open();
				luaL_openlibs(L);

				// add a delay function
				lua_pushcfunction(L, Delay);
				lua_setglobal(L, "delay");

				// execute script
				luaL_loadbuffer(L, lua_script, (uint)lua_script.Length, "program");
				lua_pcall(L, 0, 0, 0);

			}
			finally
			{
				// cleanup
				lua_close(L);
			}
		}

		static int Delay(lua_State L)
		{
			var ms = luaL_checkinteger(L, 1);        // get wait time
			Task.Delay(TimeSpan.FromMilliseconds(ms), CancelSource.Token).Wait();
			return 0;
		}
	}
}
