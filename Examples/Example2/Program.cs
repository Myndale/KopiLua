using static KopiLua.Lua;

// this example shows how to call a C# function (i.e. "sum") from Lua
namespace Example2
{
	class Program
	{
		static void Main()
		{
			// initialization
			var L = lua_open();
			luaL_openlibs(L);

			// set up a table
			lua_newtable(L);
			lua_pushstring(L, "sum");
			lua_pushcfunction(L, (_L) =>
			{
				var a = luaL_checknumber(_L, 1); // get a
				var b = luaL_checknumber(_L, 2); // get b
				var c = a + b;
				lua_pushnumber(_L, c);           // push result
				return 1;                        // number of return parameters
			});
			lua_rawset(L, -3);
			lua_setglobal(L, "foo");

			// execute script
			luaL_loadfile(L, "program.lua");
			lua_pcall(L, 0, 0, 0);

			// cleanup
			lua_close(L);
		}
	}
}
