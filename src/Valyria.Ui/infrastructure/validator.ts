interface ValidationError {
  prop: string
}

export default class Validator<T extends Record<string, any>> {
  private objectBeingValidated: T
  private validationErrors: ValidationError[]

  init(objectBeingValidated: T): void {
    this.objectBeingValidated = objectBeingValidated
    this.validationErrors = []
  }

  get isValid(): boolean {
    return this.validationErrors.length === 0
  }

  validateProperty(
    prop: keyof T,
    propIsInvalidFunc: (val: any) => boolean = (val) => val === undefined
  ): void {
    const validationIndex = this.validationErrors.findIndex((e) => e.prop === prop as string)
    const propIsInvalid = propIsInvalidFunc(this.objectBeingValidated[prop])
    const propAlreadyHasError = validationIndex > -1

    if (propIsInvalid && !propAlreadyHasError) {
      this.validationErrors.push({ prop: prop as string })
    } else if (!propIsInvalid && propAlreadyHasError) {
      this.validationErrors.splice(validationIndex, 1)
    }
  }

  propertyHasValidationError(prop: keyof T): boolean {
    return this.validationErrors.some((e) => e.prop === prop)
  }
}
