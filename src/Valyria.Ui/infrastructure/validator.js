export default class Validator {
  constructor(objectBeingValidated) {
    this.objectBeingValidated = objectBeingValidated;
    this.validationErrors = [];
  }

  get isValid() {
    return this.validationErrors.length === 0;
  }

  validateProperty(prop, propIsInvalidFunc = val => val === undefined) {
    const validationIndex = this.validationErrors.findIndex(e => e.prop === prop);
    const propIsInvalid = propIsInvalidFunc(this.objectBeingValidated[prop]);
    const propAlreadyHasError = validationIndex > -1;
    if (propIsInvalid && !propAlreadyHasError) {
      this.validationErrors.push({ prop });
    } else if (!propIsInvalid && propAlreadyHasError) {
      this.validationErrors.splice(validationIndex, 1);
    }
  }

  propertyHasValidationError(prop) {
    return this.validationErrors.some(e => e.prop === prop);
  }
}