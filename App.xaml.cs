using Elevate.Data;

namespace Elevate
{
    public partial class App : Application
    {
        private readonly ElevateDatabase _db;

        public App(ElevateDatabase db)
        {
            InitializeComponent();
            _db = db;

            // This ensures the AppShell (and your SplashPage) starts first
            MainPage = new AppShell();
        }

        protected override async void OnStart()
        {
            base.OnStart();
            // Just initialize the database and tables
            await _db.InitAsync();

            
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(MainPage);
        }
    }
}