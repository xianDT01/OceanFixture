import { AsyncPipe, DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, map, of, shareReplay } from 'rxjs';
import { SessionService } from '../../core/session.service';
import { OceanFixtureApiService } from '../../core/ocean-fixture-api.service';
import { GeographicMapComponent } from './geographic-map.component';

@Component({ selector: 'of-dashboard', standalone: true, imports: [AsyncPipe, DatePipe, FormsModule, RouterLink, GeographicMapComponent], templateUrl: './dashboard.component.html', styleUrl: './dashboard.component.scss' })
export class DashboardComponent {
  private readonly api = inject(OceanFixtureApiService);
  readonly session = inject(SessionService);
  error = '';
  readonly lanes$ = this.api.dashboard().pipe(catchError(() => { this.error = 'Unable to load your operations. Please try again.'; return of([]); }), shareReplay(1));
  readonly count$ = this.lanes$.pipe(map(lanes => lanes.reduce((total, lane) => total + lane.offers.length, 0)));
  search = ''; mapCollapsed = false;
  matches(value: string): boolean { return !this.search || value.toLowerCase().includes(this.search.toLowerCase()); }
}
