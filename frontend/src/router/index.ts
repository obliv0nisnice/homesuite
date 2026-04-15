import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from '@/views/DashboardView.vue'
import BudgetView from '@/views/BudgetView.vue'
import ShoppingListsView from '@/views/ShoppingListsView.vue'
import RecipesView from '@/views/RecipesView.vue'
import MealPlannerView from '@/views/MealPlannerView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardView },
    { path: '/budget', name: 'budget', component: BudgetView },
    { path: '/shopping-lists', name: 'shopping-lists', component: ShoppingListsView },
    { path: '/recipes', name: 'recipes', component: RecipesView },
    { path: '/meal-planner', name: 'meal-planner', component: MealPlannerView },
  ],
})

export default router
