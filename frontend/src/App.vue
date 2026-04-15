<template>
  <div class="app-shell">
    <aside class="sidebar">
      <h1>HomeSuite</h1>

      <nav>
        <RouterLink to="/">Dashboard</RouterLink>
        <RouterLink to="/budget">Budget</RouterLink>
        <RouterLink to="/shopping-lists">Einkaufsliste</RouterLink>
        <RouterLink to="/recipes">Rezepte</RouterLink>
        <RouterLink to="/meal-planner">Meal Planner</RouterLink>
        <RouterLink to="/inventory">Inventar</RouterLink>
        <RouterLink to="/catalog">Katalog</RouterLink>
      </nav>
    </aside>

    <main class="content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.app-shell {
  display: grid;
  grid-template-columns: 240px 1fr;
  min-height: 100vh;
  font-family: Arial, sans-serif;
}

.sidebar {
  padding: 24px;
  border-right: 1px solid #ddd;
}

.sidebar nav {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-top: 24px;
}

.content {
  padding: 24px;
}
</style>
