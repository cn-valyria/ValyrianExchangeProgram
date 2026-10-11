import { flushPromises } from '@vue/test-utils'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import { h, type Component } from 'vue'
import { UApp } from '#components'

/**
 * Mounts a component inside UApp, which Nuxt UI's tooltips, toasts and modals need. It's attached to the
 * document, so the helpers below can find both the component and the modals it teleports to the body.
 */
export async function mountInApp(component: Component, props: Record<string, unknown> = {}, options: { route?: string } = {}) {
  const wrapper = await mountSuspended({ render: () => h(UApp, () => h(component, props)) }, { ...options, attachTo: document.body })
  await flushPromises()
  return wrapper
}

const byText = (selector: string, text: string, root: ParentNode = document.body) =>
  Array.from(root.querySelectorAll<HTMLElement>(selector)).filter((el) => el.textContent?.trim() === text)

/** The one button (or link styled as a button) whose text is exactly `text` */
export function button(text: string, root: ParentNode = document.body): HTMLButtonElement {
  const matches = byText('button, a', text, root)
  if (matches.length !== 1) {
    throw new Error(`Expected one "${text}" button, found ${matches.length}`)
  }
  return matches[0] as HTMLButtonElement
}

/** Every button (or button-styled link) whose text is exactly `text`, in document order */
export function buttons(text: string, root: ParentNode = document.body): HTMLButtonElement[] {
  return byText('button, a', text, root) as HTMLButtonElement[]
}

export function hasButton(text: string, root: ParentNode = document.body): boolean {
  return byText('button, a', text, root).length > 0
}

export async function click(element: HTMLElement) {
  element.click()
  await flushPromises()
}

/** Types into an input or textarea the way a user would, firing the input event v-model listens to */
export async function type(element: HTMLInputElement | HTMLTextAreaElement, value: string) {
  element.value = value
  element.dispatchEvent(new Event('input', { bubbles: true }))
  await flushPromises()
}

// Compares attributes directly: happy-dom's selector engine trips over quotes inside attribute values
export function inputByPlaceholder(placeholder: string, root: ParentNode = document.body) {
  const input = Array.from(root.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>('input, textarea'))
    .find((el) => el.getAttribute('placeholder') === placeholder)
  if (!input) {
    throw new Error(`No input with placeholder "${placeholder}"`)
  }
  return input
}

export const bodyText = () => document.body.textContent ?? ''

/** Toasts shown so far (cleared between tests) */
export const toasts = () => useToast().toasts.value
