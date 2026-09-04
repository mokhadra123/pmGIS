import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

/**
 * Delete confirmation. The brief requires the user to type the Project Code before the
 * action is enabled, which makes an accidental delete of the wrong row essentially
 * impossible — the code is the one thing they cannot supply from muscle memory.
 *
 * For a bulk delete there is no single code to type, so the guard is the count instead:
 * the user types the number of projects they are about to remove.
 */
@Component({
  selector: 'app-delete-confirm',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './delete-confirm.html',
  styleUrl: './delete-confirm.scss',
})
export class DeleteConfirm {
  /** The exact text the user must type. A project code, or a count for bulk deletes. */
  readonly challenge = input.required<string>();
  readonly title = input('Delete project');
  readonly body = input('');
  readonly busy = input(false);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  protected readonly typed = signal('');

  /** Case-sensitive: project codes are uppercase by rule, so a loose match would be a lie. */
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
