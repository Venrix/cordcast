import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:tray_manager/tray_manager.dart';
import 'package:window_manager/window_manager.dart';

import 'providers/bot_provider.dart';
import 'providers/settings_provider.dart';
import 'services/app_logger.dart';
import 'services/bot_worker_service.dart';
import 'theme/app_theme.dart';
import 'widgets/app_tab_bar.dart';
import 'widgets/guilds_screen.dart';
import 'widgets/home_screen.dart';
import 'widgets/settings_screen.dart';

void main(List<String> args) {
  AppLogger.init();
  AppLogger.runGuarded(_start);
}

Future<void> _start() async {
  WidgetsFlutterBinding.ensureInitialized();
  await windowManager.ensureInitialized();

  const windowOptions = WindowOptions(
    size: Size(520, 760),
    minimumSize: Size(480, 620),
    title: 'CordCast',
    backgroundColor: AppTheme.background,
    titleBarStyle: TitleBarStyle.normal,
  );
  await windowManager.waitUntilReadyToShow(windowOptions, () async {
    await windowManager.show();
    await windowManager.focus();
  });

  final settings = SettingsProvider();
  await settings.init();

  final worker = BotWorkerService();
  final bot = BotProvider(worker, settings);

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: bot),
        ChangeNotifierProvider.value(value: settings),
      ],
      child: const CordCastApp(),
    ),
  );

  await bot.init(autoStart: settings.config.autoLogin);
}

class CordCastApp extends StatelessWidget {
  const CordCastApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'CordCast',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.darkTheme,
      home: const _Shell(),
    );
  }
}

class _Shell extends StatefulWidget {
  const _Shell();

  @override
  State<_Shell> createState() => _ShellState();
}

class _ShellState extends State<_Shell> with WindowListener, TrayListener {
  int _tabIndex = 0;

  @override
  void initState() {
    super.initState();
    windowManager.addListener(this);
    trayManager.addListener(this);
    _setupTray();
  }

  @override
  void dispose() {
    windowManager.removeListener(this);
    trayManager.removeListener(this);
    super.dispose();
  }

  Future<void> _setupTray() async {
    await trayManager.setToolTip('CordCast');
    await trayManager.setContextMenu(Menu(items: [
      MenuItem(key: 'show', label: 'Show CordCast'),
      MenuItem.separator(),
      MenuItem(key: 'quit', label: 'Quit'),
    ]));
  }

  @override
  void onWindowClose() async {
    await windowManager.hide();
  }

  @override
  void onTrayIconMouseDown() {
    windowManager.show();
    windowManager.focus();
  }

  @override
  void onTrayMenuItemClick(MenuItem menuItem) {
    if (menuItem.key == 'show') {
      windowManager.show();
      windowManager.focus();
    } else if (menuItem.key == 'quit') {
      context.read<BotProvider>().dispose();
      windowManager.destroy();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppTheme.background,
      body: Column(
        children: [
          AppTabBar(
            selectedIndex: _tabIndex,
            onTabChanged: (i) => setState(() => _tabIndex = i),
          ),
          const Divider(color: AppTheme.surfaceVariant, height: 1),
          Expanded(
            child: IndexedStack(
              index: _tabIndex,
              children: const [
                HomeScreen(),
                SettingsScreen(),
                GuildsScreen(),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
