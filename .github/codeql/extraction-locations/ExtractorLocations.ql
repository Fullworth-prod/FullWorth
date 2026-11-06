/**
 * @name C# extractor error locations
 * @description Retain source locations omitted by the standard diagnostic notification.
 * @kind table
 * @id fullworth/csharp-extractor-error-locations
 */
import csharp
import semmle.code.csharp.commons.Diagnostics

from ExtractorError error
select error.getLocation().getFile().getRelativePath() as path,
  error.getLocation().getStartLine() as start_line,
  error.getLocation().getStartColumn() as start_column,
  error.getOrigin() as origin, error.getText() as message
