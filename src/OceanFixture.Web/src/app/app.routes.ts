import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell.component';
import { OfferWorkspaceComponent } from './features/offers/offer-workspace.component';
import { ComparativeComponent } from './features/offers/comparative.component';
import { NegotiationComponent } from './features/offers/negotiation.component';
import { RecapComponent } from './features/offers/recap.component';
import { sessionGuard } from './core/session.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/session/demo-login.component').then(module => module.DemoLoginComponent), title: 'OceanFixture - Local demo profiles' },
  {
    path: '',
    component: ShellComponent,
    canActivate: [sessionGuard], canActivateChild: [sessionGuard],
    children: [
      { path: 'home', loadComponent: () => import('./features/dashboard/dashboard.component').then(module => module.DashboardComponent), title: 'OceanFixture - Home' },
      { path: 'offers/new', component: OfferWorkspaceComponent, canActivate: [sessionGuard], data: { roles: ['Charterer', 'Admin'] }, title: 'OceanFixture - New cargo order' },
      { path: 'offers/:id/comparative', component: ComparativeComponent, title: 'OceanFixture - Comparative' },
      { path: 'offers/:id/negotiation', component: NegotiationComponent, title: 'OceanFixture - Negotiation' },
      { path: 'offers/:id/recap', component: RecapComponent, title: 'OceanFixture - Fixture recap' },
      { path: 'offers/:id', component: OfferWorkspaceComponent, title: 'OceanFixture - Offer' },
      { path: '', pathMatch: 'full', redirectTo: 'home' }
    ]
  },
  { path: '**', redirectTo: 'home' }
];
