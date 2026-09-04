import type { CanDeactivateFn } from '@angular/router';

export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

//  Warns before navigating away from a form with unsaved changes.
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) => {
  if (!component?.hasUnsavedChanges?.()) {
    return true;
  }

  return window.confirm(
    'This project has unsaved changes. A draft has been kept, but leaving now will not save it to the server.\n\nLeave anyway?',
  );
};
