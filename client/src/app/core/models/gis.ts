// Results of comparing the feature layer against the database, in both directions.

// A point in the layer that no project row points at.
export interface OrphanFeature {
  readonly objectId: number;
  readonly projectCode: string | null;
}

// A project row whose ObjectId no longer exists in the layer.
export interface OrphanProjectRow {
  readonly projectId: number;
  readonly projectCode: string;
  readonly objectId: number;
}

// Linked, but the two stores disagree about the project code.
export interface CodeMismatch {
  readonly objectId: number;
  readonly databaseCode: string;
  readonly layerCode: string | null;
}

export interface ReconciliationReport {
  readonly orphanFeatures: readonly OrphanFeature[];
  readonly orphanProjectRows: readonly OrphanProjectRow[];
  readonly codeMismatches: readonly CodeMismatch[];
  readonly featuresChecked: number;
  readonly projectsChecked: number;
  readonly generatedOn: string;
  readonly isClean: boolean;
}
