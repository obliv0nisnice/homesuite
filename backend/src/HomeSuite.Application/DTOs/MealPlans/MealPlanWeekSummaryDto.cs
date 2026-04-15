namespace HomeSuite.Application.DTOs.MealPlans;

public class MealPlanWeekSummaryDto
{
    public DateOnly WeekStartDate { get; set; }
    public DateOnly WeekEndDate { get; set; }
    public List<MealPlanWeekIngredientDto> Ingredients { get; set; } = [];
}
