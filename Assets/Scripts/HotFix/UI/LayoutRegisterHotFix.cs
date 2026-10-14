using static GBH;
using static LayoutManager;

public class LayoutRegisterHotFix
{
	public static void registeAll()
	{
		// auto generate start
		registeLayout<UIMain>(script =>							mUIMain = script);
		// auto generate end
	}
}