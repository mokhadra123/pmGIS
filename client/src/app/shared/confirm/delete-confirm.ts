import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

// Delete confirmation.
@Component({
  selector: 'app-delete-confirm',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './delete-confirm.html',
  styleUrl: './delete-confirm.scss',
})
export class DeleteConfirm {
  // The exact text the user must type.
  readonly challenge = input.required<string>();
  readonly title = input('Delete project');
  readonly body = input('');
  readonly busy = input(false);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  protected readonly typed = signal('');

  // Case-sensitive: project codes are uppercase by rule, so a loose match would be a lie.
  protected readonly matches = computed(() => this.typed().trim() === this.challenge());

  protected confirm(): void {
    if (this.matches() && !this.busy()) {
      this.confirmed.emit();
    }
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape' && !this.busy()) {
      this.cancelled.emit();
    }
  }
}
