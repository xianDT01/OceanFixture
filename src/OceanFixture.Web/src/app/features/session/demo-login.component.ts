import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { SessionService, SessionUser } from '../../core/session.service';

@Component({ selector: 'of-demo-login', standalone: true,
  template: `<main class="login"><section><header><img src="assets/oceanfixture-mark.svg" alt="OceanFixture"/><h1>OceanFixture</h1></header>
    <h2>Local demo profiles</h2><p>Choose a role to test its own workflow. These are demonstration accounts, not a production login.</p>
    @if (loading) { <p role="status">Loading profiles...</p> }
    @if (error) { <p role="alert">{{ error }}</p><button (click)="load()" [disabled]="saving">RETRY</button> }
    <div class="profiles">@for (account of accounts; track account.id) { <button [disabled]="saving" (click)="choose(account)"><b>{{ account.role }}</b><span>{{ account.companyName }}</span></button> }</div>
    <small>Available only on this computer with the API running in Development. Sessions expire after 8 hours and are invalidated when the API restarts.</small>
  </section></main>`,
  styles: [`.login{min-height:100vh;background:#f3f4f6;display:grid;place-items:center;padding:24px;color:#33445b}.login section{max-width:660px;width:100%;background:white;box-shadow:0 2px 12px #0002;border-radius:5px;padding:28px}.login header{display:flex;align-items:center;gap:18px;background:#263e6d;color:white;padding:16px}.login img{width:64px;height:64px}.login h1{font-size:24px}.login p,.login small{line-height:1.6}.profiles{display:grid;gap:10px;margin:20px 0}.profiles button{display:flex;justify-content:space-between;gap:16px;padding:16px;border:1px solid #cbd6e3;background:#f8fbff;color:#263e6d;cursor:pointer;text-align:left}.profiles button:hover{border-color:#2b78d4}.profiles button:disabled{opacity:.5;cursor:wait}.login small{display:block;color:#63758c}`] })
export class DemoLoginComponent {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  accounts: SessionUser[] = [];
  loading = true;
  saving = false;
  error = '';
  constructor() { this.load(); }
  load(): void {
    this.loading = true; this.error = '';
    this.session.accounts().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: accounts => { this.accounts = accounts; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Local demo profiles are unavailable. Start the API in Development and retry.'; }
    });
  }
  choose(account: SessionUser): void {
    if (this.saving) return;
    this.saving = true; this.error = '';
    this.session.signIn(account.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving = false; void this.router.navigate(['/home']); },
      error: () => { this.saving = false; this.error = 'Unable to start this demo session.'; }
    });
  }
}
