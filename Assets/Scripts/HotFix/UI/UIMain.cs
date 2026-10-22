using Obfuz;

// auto generate member start
// generate from:Assets/GameResources/UI/UIPrefab/UIMain.prefab
// 主菜单界面
[ObfuzIgnore(ObfuzScope.TypeName)]
public class UIMain : LayoutScript
{
	protected myUGUIObject mbtn_new;
	protected myUGUIObject mbtn_continue;
	protected myUGUIObject mbtn_setting;
	protected myUGUIObject mbtn_exit;
	// auto generate member end
	public UIMain()
	{
		// auto generate constructor start
		// auto generate constructor end
	}
	public override void assignWindow()
	{
		// auto generate assignWindow start
		newObject(out mbtn_new, "btn_new");
		newObject(out mbtn_continue, "btn_continue");
		newObject(out mbtn_setting, "btn_setting");
		newObject(out mbtn_exit, "btn_exit");
		// auto generate assignWindow end
	}
	public override void init()
	{
		base.init();
		var newGameObject = mbtn_new.getGameObject();
		var newGameButton = newGameObject.GetComponent<UnityEngine.UI.Button>() ?? newGameObject.AddComponent<UnityEngine.UI.Button>();
		newGameButton.onClick.AddListener(MG01.Demo.DemoModule.Enter);
		// auto generate init start
		// auto generate init end
	}
	public override void onGameState()
	{
		base.onGameState();
	}
}
