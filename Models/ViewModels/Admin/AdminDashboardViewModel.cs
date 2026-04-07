namespace LapTopBD.Models.ViewModels.Admin
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalOrders { get; set; }
        public int TotalProduct { get; set; }

        public int TodayOrders { get; set; }
        public int WeekOrders { get; set; }
        public int MonthOrders { get; set; }
        public int OnlineVisitors { get; set; }

        public decimal TodayRevenue { get; set; }
        public decimal WeekRevenue { get; set; }
        public decimal MonthRevenue { get; set; }

        public List<string> Last7DaysLabels { get; set; } = new();
        public List<int> Last7DaysOrderCounts { get; set; } = new();
        public List<decimal> Last7DaysRevenue { get; set; } = new();

        public int SelectedMonth { get; set; }
        public int SelectedYear { get; set; }
        public int MonthlyVisits { get; set; }
        public int TotalVisits { get; set; }
        public List<string> MonthlyVisitLabels { get; set; } = new();
        public List<int> MonthlyVisitSeries { get; set; } = new();

        public List<DashboardStatItem> BrowserStats { get; set; } = new();
        public List<DashboardStatItem> DeviceStats { get; set; } = new();
        public List<DashboardStatItem> TopIps { get; set; } = new();
    }

    public class DashboardStatItem
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
