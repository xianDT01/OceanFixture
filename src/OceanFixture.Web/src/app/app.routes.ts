import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { OfferWorkspaceComponent } from './features/offers/offer-workspace.component';
import { ComparativeComponent } from './features/offers/comparative.component';

export const routes: Routes = [
  {
    path: '',
    component: ShellComponent,
    children: [
      { path: 'home', component: DashboardComponent, title: 'OceanFixture - Home' },
      { path: 'offers/new', component: OfferWorkspaceComponent, title: 'OceanFixture - New cargo order' },
      { path: 'offers/:id/comparative', component: ComparativeComponent, title: 'OceanFixture - Comparative' },
      { path: 'offers/:id', component: OfferWorkspaceComponent, title: 'OceanFixture - Offer' },
      { path: '', pathMatch: 'full', redirectTo: 'home' }
    ]
  },
  { path: '**', redirectTo: 'home' }
];
