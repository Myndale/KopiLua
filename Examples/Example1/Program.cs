using static KopiLua.Lua;

// this example shows how to call a Lua function (i.e. "sum") from C#.
namespace Example1
{
	class Program
	{
		static void Main()
		{
			// initialization
			var L = lua_open();
			luaL_openlibs(L);

			// execute script
			var lua_script = "function sum(a, b) return a+b; end"; // a function that returns sum of two
			luaL_loadbuffer(L, lua_script, (uint)lua_script.Length, "program");
			lua_pcall(L, 0, 0, 0);

			// load the function from global
			lua_getglobal(L, "sum");
			if (lua_isfunction(L, -1))
			{
				// push function arguments into stack
				lua_pushnumber(L, 5.0);
				lua_pushnumber(L, 6.0);
				lua_pcall(L, 2, 1, 0);
				double sumval = 0.0;
				if (!lua_isnil(L, -1))
				{
					sumval = lua_tonumber(L, -1);
					lua_pop(L, 1);
				}

				// note that C-style printf is available via the KopiLua library
				printf("sum=%lf\n", sumval);
			}

			// cleanup
			lua_close(L);
		}
	}
}
